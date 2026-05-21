using ChurrunKubernetes.Models.Dtos.Environment;
using DispatchR.Abstractions.Send;
using LazyCache;
using Microsoft.Extensions.Caching.Memory;
using System.Text.Json;
using YamlDotNet.Serialization;
using YamlDotNet.System.Text.Json;

namespace ChurrunKubernetes.Commands.Environment
{
    public class GetEnvironmentHandler : IRequestHandler<GetEnvironment, ValueTask<EnvironmentDefinition>>
    {
        private readonly IConfiguration _configuration;
        private readonly IAppCache _appCache;

        public GetEnvironmentHandler(IConfiguration configuration, IAppCache appCache)
        {
            _configuration = configuration;
            _appCache = appCache;
        }

        public async ValueTask<EnvironmentDefinition> Handle(GetEnvironment request, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(_configuration["Name"]))
                throw new ArgumentException("Environment name is not configured.");

            return await _appCache.GetOrAddAsync("envInfo", async ctx =>
            {
                ctx.SetAbsoluteExpiration(TimeSpan.FromMinutes(1));
                var capabilities = new Dictionary<string, string>
                {
                    { "namespace", _configuration["Kubernetes:Namespace"] ?? "none" },
                    { "service_mesh", _configuration["Kubernetes:ServiceMesh"] ?? "none" },
                    { "user_storage_class", _configuration["Kubernetes:StorageClass"] ?? "" },
                    { "shared_storage_class", _configuration["Kubernetes:SharedStorageClass"] ?? "" }
                };

                var limits = new QuotaDefinition(
                    cpu: _configuration["Kubernetes:Limits:Cpu"],
                    memory: _configuration["Kubernetes:Limits:Memory"],
                    gpu: _configuration["Kubernetes:Limits:Gpu"],
                    storage: _configuration["Kubernetes:Limits:Storage"]
                );

                var sizes = new List<SizeDefinition>();

                var sizeFile = "/app/sizes.yaml";
#if DEBUG
                sizeFile = "./sizes.yaml";
#endif
                if (File.Exists(sizeFile))
                {
                    var rawSizes = await File.ReadAllTextAsync(sizeFile, cancellationToken);
                    var yamlDeserializer = new DeserializerBuilder()
                        .AddSystemTextJson()
                        .Build();
                    var jsonSizes = yamlDeserializer.Deserialize<JsonElement>(rawSizes);
                    if (jsonSizes.TryGetProperty("sizes", out var sizesElement))
                    {
                        var sizeDefinitions = JsonSerializer.Deserialize<SizeDefinition[]>(sizesElement.GetRawText(), JsonSettings.Value);
                        if (sizeDefinitions != null)
                        {
                            sizes.AddRange(sizeDefinitions);
                        }
                    }
                }

                if (sizes.Count == 0)
                {
                    sizes.Add(new SizeDefinition("500mx1Gi", "Tiny (0.5 vCPU 1 GB RAM)", "A tiny server for hosting lightweigt web apps",
                        requests: new QuotaDefinition("0.1", "64Mi", "", "1Gi"),
                        limits: new QuotaDefinition("0.5", "1Gi", "", "1Gi"),
                        translation: null));
                }

                var result = new EnvironmentDefinition(
                    name: _configuration["Name"] ?? "dev",
                    basePath: "/share",
                    capabilities: capabilities,
                    description: "Kubernetes based environment",
                    limits: limits,
                    sizes: sizes.ToArray(),
                    translation: null
                );

                return result;
            });
        }
    }
}
