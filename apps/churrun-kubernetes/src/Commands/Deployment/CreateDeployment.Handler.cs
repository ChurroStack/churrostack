using ChurrunKubernetes.Commands.Environment;
using ChurrunKubernetes.Commands.Template;
using ChurrunKubernetes.Data;
using ChurrunKubernetes.Models.Dtos.Deployment;
using ChurrunKubernetes.Models.Dtos.Exceptions;
using ChurrunKubernetes.Services;
using ChurrunKubernetes.Services.Share;
using ChurrunKubernetes.Utils;
using DispatchR;
using DispatchR.Abstractions.Send;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace ChurrunKubernetes.Commands.Deployment
{
    public class CreateDeploymentHandler : IRequestHandler<CreateDeployment, ValueTask<DeploymentSummary>>
    {
        private readonly IMediator _mediator;
        private readonly TemplateService _templateService;
        private readonly KubernetesService _kubernetesService;
        private readonly ChurrunDbContext _dbContext;
        private readonly ProxyConfigurationProvider _proxyConfigurationProvider;
        private readonly ILogger<CreateDeploymentHandler> _logger;
        private static object _obj = new object();

        public CreateDeploymentHandler(IMediator mediator, TemplateService templateService, KubernetesService kubernetesService, ProxyConfigurationProvider proxyConfigurationProvider, ChurrunDbContext dbContext, ILogger<CreateDeploymentHandler> logger)
        {
            _mediator = mediator;
            _templateService = templateService;
            _kubernetesService = kubernetesService;
            _proxyConfigurationProvider = proxyConfigurationProvider;
            _dbContext = dbContext;
            _logger = logger;
        }

        public async ValueTask<DeploymentSummary> Handle(CreateDeployment request, CancellationToken cancellationToken)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(request.Body.Name, nameof(request.Body.Name));
            ArgumentException.ThrowIfNullOrWhiteSpace(request.Body.Template, nameof(request.Body.Template));

            if (!NamingUtils.IsValidName(request.Body.Name))
            {
                throw new ArgumentException("The provided name is not valid. Only lowercase alphanumeric and hypens (-) are allowed.", nameof(request.Body.Name));
            }
            if (request.Body.Extensions is not null)
            {
                foreach (var extension in request.Body.Extensions)
                {
                    var extensionName = extension.Name;
                    if (!NamingUtils.IsValidName(extensionName))
                    {
                        throw new ArgumentException("The provided extension name is not valid. Only lowercase alphanumeric and hypens (-) are allowed.", nameof(extensionName));
                    }
                }
            }

            // Obtain base template and extensions
            var templateItem = await _mediator.Send(new GetTemplate(request.Body.Template), cancellationToken);
            request.Body.Template = $"{request.Body.Template}/{Convert.ToBase64String(templateItem.Hash)}";

            var templateRawContent = templateItem.Content;
            ArgumentException.ThrowIfNullOrWhiteSpace(templateRawContent, nameof(templateRawContent));
            var templateJson = await _templateService.EvaluateAsync(templateRawContent);
            var templateType = templateJson.GetProperty("type").GetString()!;
            if (!templateType.Equals("application") && !templateType.Equals("dependency"))
            {
                throw new HttpException(400, $"Template '{request.Body.Template}' is not an application template.");
            }

            var extensions = new List<(string Name, string TemplateName, string RawTemplate, JsonElement TemplateDefinition, IDictionary<string, string[]>? Parameters)>();

            if (templateJson.TryGetProperty("extensions", out var extensionsElement) && extensionsElement.ValueKind == JsonValueKind.Array)
            {
                foreach (var templateExtension in extensionsElement.EnumerateArray())
                {
                    var extensionName = templateExtension.GetProperty("name").GetString()!;
                    var extensionTemplate = templateExtension.GetProperty("template").GetString()!;
                    var parameters = new Dictionary<string, string[]>();
                    var extensionTemplateItem = await _mediator.Send(new GetTemplate(extensionTemplate), cancellationToken);
                    var extensionTemplateJson = await _templateService.EvaluateAsync(extensionTemplateItem.Content);
                    var extensionType = extensionTemplateJson.GetProperty("type").GetString()!;
                    if (!extensionType.Equals("extension"))
                    {
                        throw new HttpException(400, $"Template '{request.Body.Template}' is not an extension template.");
                    }

                    if (templateExtension.TryGetProperty("required", out var jsonExtensionRequired) && jsonExtensionRequired.GetBoolean())
                    {
                        extensions.Add((extensionName, extensionTemplate, extensionTemplateItem.Content, extensionTemplateJson, parameters));
                    }
                }
            }
            if (request.Body.Extensions is not null && request.Body.Extensions.Length > 0)
            {
                foreach (var extensionRequest in request.Body.Extensions)
                {
                    var existingExtension = extensions.FirstOrDefault(o => o.Name.Equals(extensionRequest.Name, StringComparison.OrdinalIgnoreCase));
                    if (existingExtension.Name is null)
                    {
                        var extensionTemplateItem = await _mediator.Send(new GetTemplate(extensionRequest.Template), cancellationToken);
                        extensionRequest.Template = $"{extensionRequest.Template}/{Convert.ToBase64String(extensionTemplateItem.Hash)}";
                        var extensionTemplateJson = await _templateService.EvaluateAsync(extensionTemplateItem.Content);
                        var extensionType = extensionTemplateJson.GetProperty("type").GetString()!;
                        if (!extensionType.Equals("extension"))
                        {
                            throw new HttpException(400, $"Template '{request.Body.Template}' is not an extension template.");
                        }

                        extensions.Add((extensionRequest.Name, extensionRequest.Template, extensionTemplateItem.Content, extensionTemplateJson, extensionRequest.Parameters));
                    }
                    else if (extensionRequest.Parameters is not null)
                    {
                        foreach (var param in extensionRequest.Parameters)
                        {
                            if (!existingExtension.Parameters!.TryAdd(param.Key, param.Value))
                            {
                                existingExtension.Parameters![param.Key] = param.Value;
                            }
                        }
                    }
                }
            }

            // Get environment info
            var environment = await _mediator.Send(new GetEnvironment(), cancellationToken);
            var basePath = $"/share/{request.Body.AppName}";

            // Defense in depth: any extension requesting a hostPath mount must target a
            // managed path declared in the environment catalog. The runner has no
            // ChurroStack identity, so per-user access is enforced by the API at save
            // time; here we only ensure the path is one the environment exposes.
            var managedHostPaths = new HashSet<string>(
                (environment.HostPaths ?? []).Select(o => o.Path),
                StringComparer.Ordinal);
            foreach (var extension in extensions)
            {
                if (extension.Parameters is null ||
                    !extension.Parameters.TryGetValue("hostPath", out var hostPathValues))
                {
                    continue;
                }
                var requestedHostPath = hostPathValues?.FirstOrDefault();
                if (string.IsNullOrWhiteSpace(requestedHostPath))
                {
                    continue;
                }
                if (!managedHostPaths.Contains(requestedHostPath))
                {
                    _logger.LogWarning("Storage hostPath denied app={AppName} deployment={Deployment} extension={Extension} path={HostPath} reason=unmanaged",
                        request.Body.AppName, request.Body.Name, extension.Name, requestedHostPath);
                    throw new HttpException(403, $"Host path '{requestedHostPath}' is not allowed in this environment.");
                }
                _logger.LogInformation("Storage hostPath allowed app={AppName} deployment={Deployment} extension={Extension} path={HostPath}",
                    request.Body.AppName, request.Body.Name, extension.Name, requestedHostPath);
            }

            // Render base template
            var jsonBaseArgs = new JsonObject();
            jsonBaseArgs["id"] = request.Body.Name;
            jsonBaseArgs["applicationName"] = request.Body.AppName;
            jsonBaseArgs["basePath"] = basePath;
            jsonBaseArgs["namespace"] = environment.Capabilities["namespace"];
            jsonBaseArgs["storageClass"] = string.IsNullOrWhiteSpace(environment.Capabilities["user_storage_class"]) ? "hostPath" : environment.Capabilities["user_storage_class"];
            jsonBaseArgs["sharedStorageClass"] = string.IsNullOrWhiteSpace(environment.Capabilities["shared_storage_class"]) ? "hostPath" : environment.Capabilities["shared_storage_class"];
            jsonBaseArgs["template"] = JsonSerializer.SerializeToNode(templateJson, JsonSettings.Value);
            jsonBaseArgs["variables"] = JsonSerializer.SerializeToNode(request.Body.Variables ?? [], JsonSettings.Value);
            jsonBaseArgs["parameters"] = JsonSerializer.SerializeToNode(NormalizeParameters(request.Body.Parameters ?? new Dictionary<string, string[]>()), JsonSettings.Value);
            jsonBaseArgs["environment"] = JsonSerializer.SerializeToNode(environment, JsonSettings.Value);
            var yamlBaseTemplate = await _templateService.TransformAsync(templateRawContent, jsonBaseArgs.Deserialize<JsonElement>());
            string manifests;

            // Render extension templates
            if (extensions is not null && extensions.Count > 0)
            {
                var patches = new List<string>();
                foreach (var extension in extensions)
                {
                    var jsonExtensionArgs = new JsonObject();
                    jsonExtensionArgs["id"] = extension.Name;
                    jsonExtensionArgs["applicationName"] = request.Body.AppName;
                    jsonExtensionArgs["basePath"] = basePath;
                    jsonExtensionArgs["target"] = request.Body.Name;
                    jsonExtensionArgs["namespace"] = environment.Capabilities["namespace"]!;
                    jsonExtensionArgs["storageClass"] = string.IsNullOrWhiteSpace(environment.Capabilities["user_storage_class"]) ? "hostPath" : environment.Capabilities["user_storage_class"];
                    jsonExtensionArgs["sharedStorageClass"] = string.IsNullOrWhiteSpace(environment.Capabilities["shared_storage_class"]) ? "hostPath" : environment.Capabilities["shared_storage_class"];
                    jsonExtensionArgs["template"] = JsonSerializer.SerializeToNode(extension.TemplateDefinition, JsonSettings.Value);
                    jsonExtensionArgs["parameters"] = JsonSerializer.SerializeToNode(NormalizeParameters(extension.Parameters), JsonSettings.Value);
                    jsonExtensionArgs["environment"] = JsonSerializer.SerializeToNode(environment, JsonSettings.Value);
                    var yamlExtensionTemplate = await _templateService.TransformAsync(extension.RawTemplate, jsonExtensionArgs.Deserialize<JsonElement>());
                    patches.Add(yamlExtensionTemplate);
                }

                var size = environment.Sizes?.FirstOrDefault(s => s.Name.Equals(request.Body.Size?.Hint ?? "", StringComparison.OrdinalIgnoreCase));
                if (size is null)
                {
                    size = environment.Sizes?.FirstOrDefault(s =>
                        (string.IsNullOrWhiteSpace(request.Body.Size?.Cpu) || (request.Body.Size?.Cpu.Equals(s.Limits?.Cpu, StringComparison.InvariantCultureIgnoreCase) ?? false)) &&
                        (string.IsNullOrWhiteSpace(request.Body.Size?.Memory) || (request.Body.Size?.Memory.Equals(s.Limits?.Memory, StringComparison.InvariantCultureIgnoreCase) ?? false)) &&
                        (string.IsNullOrWhiteSpace(request.Body.Size?.Storage) || (request.Body.Size?.Storage.Equals(s.Limits?.Storage, StringComparison.InvariantCultureIgnoreCase) ?? false)) &&
                        (string.IsNullOrWhiteSpace(request.Body.Size?.Gpu) || (request.Body.Size?.Gpu.Equals(s.Limits?.Gpu, StringComparison.InvariantCultureIgnoreCase) ?? false))
                    );
                }
                if (size is null)
                {
                    throw new ArgumentException($"Size not found in this environment.");
                }

                var yamlSizePatch = await _templateService.TransformAsync(await System.IO.File.ReadAllTextAsync("Resources/size.yaml"), JsonSerializer.SerializeToElement(new
                {
                    id = request.Body.Name,
                    @namespace = environment.Capabilities["namespace"]!,
                    requests = size.Requests,
                    limits = size.Limits
                }, JsonSettings.Value));
                patches.Add(yamlSizePatch);

                manifests = string.Join("\r\n---\r\n", await _templateService.PatchYamlAsync(yamlBaseTemplate, patches));
            }
            else
            {
                manifests = yamlBaseTemplate;
            }

            var deploymentHash = JsonSerializer.Serialize(request.Body, JsonSettings.Value).GetSha1Hash();

            if (!request.Dry)
            {
                // Common annotation for all manifests           
                var annotations = new Dictionary<string, string>
                {
                    { "churrostack.com/deployment-id", request.Body.Name },
                    { "churrostack.com/app-id", request.Body.AppName },
                    { "churrostack.com/app-hash", Convert.ToBase64String(deploymentHash) },
                    { "churrostack.com/template-id", templateItem.Name },
                    { "churrostack.com/template-hash", Convert.ToBase64String(templateItem.Hash) },
                };

                await _kubernetesService.ApplyYamlManifests(manifests, annotations);

                var deployment = _dbContext.Set<Domain.Deployment>().Find(request.Body.Name);
                if (deployment is null)
                {
                    deployment = new Domain.Deployment(request.Body.Name, request.Body.AppName, request.Body.Size, request.Body.Ports ?? []);
                    _dbContext.Set<Domain.Deployment>().Add(deployment);
                }
                else
                {
                    deployment.Ports = request.Body.Ports ?? [];
                }

                await _dbContext.SaveChangesAsync(cancellationToken);

                lock (_obj)
                {
                    var deploymentNames = _dbContext
                        .Set<Domain.Deployment>()
                        .Where(o => o.AppName == request.Body.AppName)
                        .Select(o => o.Name)
                        .ToArray();
                    _proxyConfigurationProvider.AddProxy(deployment.AppName, deploymentNames, deployment.Ports);
                    _proxyConfigurationProvider.Reload();
                }
            }

            return new DeploymentSummary(request.Body.Name, deploymentHash, templateItem.Name, DateTimeOffset.UtcNow);
        }

        private IDictionary<string, object> NormalizeParameters(IDictionary<string, string[]>? dictionary)
        {
            var result = new Dictionary<string, object>();
            if (dictionary is null || dictionary.Count == 0)
            {
                return result;
            }
            foreach (var item in dictionary)
            {
                if (item.Value is null)
                {
                    continue;
                }
                else if (item.Value.Length == 1)
                {
                    result.TryAdd(item.Key, item.Value[0]);
                }
                else
                {
                    // TODO: Check isMulti (because single value multi can cause problems)
                    result.TryAdd(item.Key, item.Value);
                }
            }
            return result;
        }
    }
}
