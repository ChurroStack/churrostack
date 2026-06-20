using ChurrOS.Api.Commands.Environment;
using ChurrOS.Api.Commands.Identity;
using ChurrOS.Api.Commands.Template;
using ChurrOS.Api.Data;
using ChurrOS.Api.Domain;
using ChurrOS.Api.Models.Dtos.Deployment;
using ChurrOS.Api.Models.Dtos.Identity;
using ChurrOS.Api.Models.Dtos.Template.Definition;
using ChurrOS.Api.Services;
using ChurrOS.Api.Services.Security;
using ChurrOS.Api.Services.Share;
using ChurrOS.Api.Utils.Exceptions;
using DispatchR;
using DispatchR.Abstractions.Send;
using Microsoft.EntityFrameworkCore;
using System.CommandLine.Parsing;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace ChurrOS.Api.Commands.Applications
{
    public class DeployApplicationHandler : IRequestHandler<DeployApplication, ValueTask<DeploymentSummary[]>>
    {
        private readonly IMediator _mediator;
        private readonly ChurrosDbContext _context;
        private readonly RunnerService _runnerService;
        private readonly ProxyConfigurationProvider _proxyConfigurationProvider;
        private readonly TemplateService _templateService;
        private readonly ILockService _lockService;
        private readonly ITenantResolver _tenantResolver;

        public DeployApplicationHandler(
            IMediator mediator,
            ChurrosDbContext context,
            RunnerService runnerService,
            ProxyConfigurationProvider proxyConfigurationProvider,
            TemplateService templateService,
            ILockService lockService,
            ITenantResolver tenantResolver)
        {
            _mediator = mediator;
            _context = context;
            _runnerService = runnerService;
            _proxyConfigurationProvider = proxyConfigurationProvider;
            _templateService = templateService;
            _lockService = lockService;
            _tenantResolver = tenantResolver;
        }

        public async ValueTask<DeploymentSummary[]> Handle(DeployApplication request, CancellationToken cancellationToken)
        {
            var repo = _context.Set<Domain.Application>();

            var app = await repo
                .Include(o => o.Extensions)
                .Include(o => o.Deployments)
                .Include(o => o.Environment)
                .Include(o => o.Template)
                .Include(o => o.CreatedBy)
                .Include(o => o.ModifiedBy)
                .FirstOrDefaultAsync(o => o.Name == request.Name);

            if (app == null)
            {
                throw new NotFoundException($"Application with name '{request.Name}' was not found.");
            }

            var isAdmin = await _mediator.Send(new HasRole(IdentityRole.Administrator, _context.IdentityId), cancellationToken);
            if (!isAdmin)
            {
                var identityAcls = await _mediator.Send(new GetIdentityAcls(_context.IdentityId, Permission.Write), cancellationToken);
                if (!identityAcls.ContainsKey(app.AclId) && !identityAcls.ContainsKey(app.Environment!.AclId))
                    throw new UnauthorizedAccessException("You do not have permission to deploy this application.");
            }

            var parts = app.Environment!.EncryptionKey.Split(':');
            var encryptionKey = AesGcmEncryption.Decrypt(parts[0], _context.AccountEncryptionKey, parts[1]);
            var client = _runnerService.CreateClient(app.Environment!.Host[1], app.Environment.Name, app.Environment.Port, encryptionKey);

            var appTemplate = app.Template!;
            var appParameters = ParseParameters(true, app.Name, appTemplate.Definition.Parameters, app.Parameters, null);
            var extensions = new List<DeploymentExtensionRequestItem>();
            // Re-render ports from the template/extension definitions on every deploy so template
            // changes (e.g. a changed port number or transform) propagate to already-deployed
            // applications. Rebuild the list from the current definitions and carry over only the
            // user-editable Authentication/Sharing from the previously stored ports. (Previously an
            // already-present port name was skipped, so it kept its stale value forever.)
            var existingPorts = app.Ports?.ToList() ?? new List<PortDefinition>();
            var ports = new List<PortDefinition>();
            var basePath = $"/share/{app.Name}";

            if (appTemplate.Definition.Ports is not null)
            {
                foreach (var templatePort in appTemplate.Definition.Ports)
                {
                    if (ports.Any(p => p.Name == templatePort.Name))
                        continue;
                    var jsonBaseArgs = new JsonObject();
                    jsonBaseArgs["id"] = templatePort.Name;
                    jsonBaseArgs["applicationName"] = app.Name;
                    jsonBaseArgs["basePath"] = basePath;
                    templatePort.Transforms = await TransformTransforms(templatePort, jsonBaseArgs);
                    if (!string.IsNullOrWhiteSpace(templatePort.Uri))
                    {
                        templatePort.Uri = await _templateService.TransformAsync(templatePort.Uri, jsonBaseArgs.Deserialize<JsonElement>());
                    }
                    var existingPort = existingPorts.FirstOrDefault(p => p.Name == templatePort.Name);
                    if (existingPort is not null)
                    {
                        templatePort.Authentication = existingPort.Authentication;
                        templatePort.Sharing = existingPort.Sharing;
                    }
                    ports.Add(templatePort);
                }
            }

            if (app.Extensions is not null)
            {
                foreach (var appExtension in app.Extensions)
                {
                    if (!appExtension.Enabled)
                        continue;

                    var extensionTemplate = await _mediator.Send(new GetTemplateByName(appExtension.TemplateId.ToString(), app.Environment!.Type), cancellationToken);
                    var extensionParameters = ParseParameters(appExtension.Enabled, appExtension.Name, extensionTemplate.Definition.Parameters, appExtension.Parameters, null);
                    extensions.Add(new DeploymentExtensionRequestItem(appExtension.Name, extensionTemplate.Name, extensionParameters));
                    if (extensionTemplate.Definition.Ports is not null)
                    {
                        foreach (var templatePort in extensionTemplate.Definition.Ports)
                        {
                            if (ports.Any(p => p.Name == templatePort.Name))
                                continue;
                            var jsonBaseArgs = new JsonObject();
                            jsonBaseArgs["id"] = templatePort.Name;
                            jsonBaseArgs["applicationName"] = app.Name;
                            jsonBaseArgs["basePath"] = basePath;
                            templatePort.Transforms = await TransformTransforms(templatePort, jsonBaseArgs);
                            if (!string.IsNullOrWhiteSpace(templatePort.Uri))
                            {
                                templatePort.Uri = await _templateService.TransformAsync(templatePort.Uri, jsonBaseArgs.Deserialize<JsonElement>());
                            }
                            var existingPort = existingPorts.FirstOrDefault(p => p.Name == templatePort.Name);
                            if (existingPort is not null)
                            {
                                templatePort.Authentication = existingPort.Authentication;
                                templatePort.Sharing = existingPort.Sharing;
                            }
                            ports.Add(templatePort);
                        }
                    }
                }
            }

            var now = DateTimeOffset.Now;
            app.Ports = ports.ToArray();

            ApplicationDeployment? deployment;
            long deploymentOwnerId;
            if (string.IsNullOrWhiteSpace(request.DeploymentName))
            {
                deploymentOwnerId = request.DeploymentOwnerId ?? _context.IdentityId;
                if (app.Mode == Models.Dtos.Application.ApplicationMode.Application)
                {
                    deployment = app.Deployments?.FirstOrDefault();
                }
                else
                {
                    deployment = app.Deployments?.FirstOrDefault(o => o.OwnerId == deploymentOwnerId);
                }
            }
            else
            {
                deployment = app.Deployments?.FirstOrDefault(o => o.Name == request.DeploymentName);
                if (deployment is null)
                {
                    throw new ArgumentException($"Deployment with name '{request.DeploymentName}' was not found for application '{app.Name}'.");
                }
                deploymentOwnerId = deployment.OwnerId ?? _context.IdentityId;
            }

            var deploymentName = app.Mode == Models.Dtos.Application.ApplicationMode.Workspace ? $"{app.Name}-{deploymentOwnerId}" : app.Name;
            var deployRequest = new DeploymentRequestItem(deploymentName, app.Name, appTemplate.Name!, app.Replicas, app.Size, appParameters, extensions.ToArray(), ports.ToArray(), app.Variables);

            // Templates render with replicas: 1, so applying the manifest schedules a pod
            // immediately even though the row's ExecutionStatus stays Stopped until
            // ScrapeDeploymentStateJob reconciles it. Treat that pod scheduling as a Start for
            // quota purposes whenever the target deployment isn't already in the running set
            // (new deployment, or existing one Stopped/Stopping). Re-deploying a Running/Starting
            // deployment is a no-op for the running totals and skips the check.
            var addsRunningInstance = deployment is null
                || deployment.ExecutionStatus == DeploymentExecutionStatus.Stopped
                || deployment.ExecutionStatus == DeploymentExecutionStatus.Stopping;

            IAsyncDisposable? envLock = null;
            if (addsRunningInstance)
            {
                var lockKey = $"churros_tenant:{_tenantResolver.AccountId}:env:{app.EnvironmentId}:resource_lock";
                envLock = await _lockService.AcquireAsync(lockKey, TimeSpan.FromSeconds(120), TimeSpan.FromSeconds(5), cancellationToken)
                    ?? throw new InvalidOperationException("Environment is busy, please retry.");
                await _mediator.Send(new EnsureEnvironmentRunningQuota(app.EnvironmentId, app.Id, app.Size, EnsureRunningQuotaMode.Start), cancellationToken);
            }

            try
            {
                var result = await client.DeployAsync(deployRequest, cancellationToken);

                // When the deploy will add a running instance, persist ExecutionStatus = Starting
                // before releasing the env lock so any concurrent Deploy/Start/Update that takes
                // the lock immediately after counts this pod's contribution against the env budget.
                // ScrapeDeploymentStateJob later reconciles to Running or back to Stopped based on
                // actual replica state. Without this, the lock-through-SaveChanges pattern leaks:
                // the row would be saved as Stopped and EnsureEnvironmentRunningQuota — which
                // filters on Running/Starting — would miss it, allowing two concurrent Deploys to
                // each pass a check that should have caught the second.
                var newExecutionStatus = addsRunningInstance
                    ? DeploymentExecutionStatus.Starting
                    : (deployment?.ExecutionStatus ?? DeploymentExecutionStatus.Stopped);

                if (deployment is null)
                {
                    _context.Add(new Domain.ApplicationDeployment(
                        accountId: app.AccountId,
                        applicationId: app.Id,
                        deploymentHash: result.Hash,
                        name: deploymentName,
                        ownerId: app.Mode == Models.Dtos.Application.ApplicationMode.Workspace ? deploymentOwnerId : null,
                        provisionStatus: DeploymentProvisionStatus.Provisioning,
                        executionStatus: newExecutionStatus,
                        deploymentStatus: null,
                        deployedAt: now,
                        tags: Array.Empty<string>(),
                        metadata: null,
                        createdAt: now,
                        createdById: _context.IdentityId,
                        modifiedAt: now,
                        modifiedById: _context.IdentityId
                    ));
                }
                else
                {
                    deployment.ProvisionStatus = DeploymentProvisionStatus.Provisioning;
                    deployment.ExecutionStatus = newExecutionStatus;
                    deployment.DeploymentHash = result.Hash;
                    deployment.DeployedAt = now;
                    deployment.ModifiedAt = now;
                    deployment.ModifiedById = _context.IdentityId;
                    deployment.DeploymentStatus = null;
                    _context.Update(deployment);
                }

                await _context.SaveChangesAsync(cancellationToken);

                if (app.Ports.Any())
                {
                    _proxyConfigurationProvider.AddApplication(app.Name, app.Ports, app.Environment.Name);
                    _proxyConfigurationProvider.Reload();
                }

                return [result];
            }
            finally
            {
                if (envLock is not null)
                    await envLock.DisposeAsync();
            }
        }

        private async Task<IList<IDictionary<string, string>>?> TransformTransforms(PortDefinition templatePort, JsonObject jsonBaseArgs)
        {
            IList<IDictionary<string, string>>? newTransforms = null;
            if (templatePort.Transforms?.Any() ?? false)
            {
                newTransforms = new List<IDictionary<string, string>>();
                foreach (var transform in templatePort.Transforms)
                {
                    var newDict = new Dictionary<string, string>();
                    foreach (var kvp in transform)
                    {
                        var key = kvp.Key;
                        key = char.ToUpper(key[0]) + key.Substring(1);
                        newDict[key] = await _templateService.TransformAsync(kvp.Value, jsonBaseArgs.Deserialize<JsonElement>());
                    }
                    newTransforms.Add(newDict);
                }
            }
            return newTransforms;
        }

        internal static IDictionary<string, string[]> ParseParameters(bool extensionEnabled, string name, IDictionary<string, ParameterDefinition>? definition, IDictionary<string, string[]>? requestParameters, IDictionary<string, string[]>? extensionParameters)
        {
            var result = new Dictionary<string, string[]>();
            if (definition is not null)
            {
                foreach (var templateParameter in definition)
                {
                    if (requestParameters?.TryGetValue(templateParameter.Key, out var paramValue) ?? false)
                    {
                        if (paramValue?.Length > 0)
                        {
                            result.TryAdd(templateParameter.Key, TransformParameterValue(paramValue, templateParameter.Value));
                        }
                    }
                    else if (extensionParameters?.TryGetValue(templateParameter.Key, out paramValue) ?? false)
                    {
                        if (paramValue?.Length > 0)
                        {
                            result.TryAdd(templateParameter.Key, TransformParameterValue(paramValue, templateParameter.Value));
                        }
                    }
                    else
                    {
                        if (templateParameter.Value.DefaultValue is not null && templateParameter.Value.DefaultValue.Length > 0)
                        {
                            result.TryAdd(templateParameter.Key, TransformParameterValue(templateParameter.Value.DefaultValue, templateParameter.Value));
                            continue;
                        }
                        if (templateParameter.Value.Required && extensionEnabled)
                        {
                            throw new InvalidOperationException($"Missing required parameter '{templateParameter.Key}' for '{name}'.");
                        }
                    }
                }
            }
            return result;
        }

        private static string[] TransformParameterValue(string[] value, ParameterDefinition definition)
        {
            switch (definition.Transformer?.ToLowerInvariant())
            {
                case "cmdargs":
                    {
                        if (value is null || value.Length == 0 || string.IsNullOrWhiteSpace(value[0]))
                            return [""];
                        return CommandLineParser.SplitCommandLine(value[0]).ToArray();
                    }
                case "":
                case null:
                    return value;
                default:
                    throw new ArgumentException($"Unknown transformer '{definition.Transformer}'");

            }
        }
    }
}
