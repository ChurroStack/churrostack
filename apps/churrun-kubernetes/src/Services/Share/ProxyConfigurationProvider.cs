using ChurrunKubernetes.Data;
using ChurrunKubernetes.Domain;
using ChurrunKubernetes.Models.Dtos.Deployment;
using ChurrunKubernetes.Models.Proxy;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Primitives;
using Yarp.ReverseProxy.Configuration;
using Yarp.ReverseProxy.Transforms;

namespace ChurrunKubernetes.Services.Share
{
    public class ProxyConfigurationProvider : IProxyConfigProvider
    {
        private readonly object _lock = new();

        private List<RouteConfig> _routes = new();
        private List<ClusterConfig> _clusters = new();

        private CancellationTokenSource _cts = new();

        private readonly IServiceScopeFactory _serviceScopeFactory;
        private readonly IConfiguration _configuration;

        public ProxyConfigurationProvider(IServiceScopeFactory serviceScopeFactory, IConfiguration configuration)
        {
            _serviceScopeFactory = serviceScopeFactory;
            _configuration = configuration;
        }

        public IProxyConfig GetConfig()
        {
            return new ProxyConfig(_routes, _clusters, new CancellationChangeToken(_cts.Token));
        }

        public async Task Initialize()
        {
            using var scope = _serviceScopeFactory.CreateScope();
            var deployments = await scope.ServiceProvider.GetRequiredService<ChurrunDbContext>().Set<Deployment>().ToListAsync();
            foreach (var appGroup in deployments.GroupBy(o => o.AppName))
            {
                var first = appGroup.First();
                AddProxy(appGroup.Key, appGroup.Select(o => o.Name).ToArray(), first.Ports);
            }
            Reload();
        }

        public void RemoveProxy(string deploymentName)
        {
            lock (_lock)
            {
                _routes.RemoveAll(r => r.RouteId.StartsWith($"{deploymentName}:"));
                _clusters.RemoveAll(c => c.ClusterId.StartsWith($"{deploymentName}:"));
                Reload();
            }
        }

        public void AddProxy(string appName, string[] destinations, PortDefinition[] ports)
        {
            foreach (var port in ports)
            {
                var key = $"{appName}:{port.Name}";
                IReadOnlyList<IReadOnlyDictionary<string, string>>? transforms = null;
                if (port.Transforms is not null && port.Transforms.Any())
                {
                    transforms = port.Transforms.Select(t => (IReadOnlyDictionary<string, string>)t.ToDictionary(o => char.ToUpper(o.Key[0]) + o.Key.Substring(1), o => o.Value)).ToList();
                }
                AddOrUpdateProxy(
                    id: key,
                    path: $"/share/{appName}/{port.Name}/{{**catch-all}}",
                    destinationAddresses: destinations.Select(destination =>
                    {
#if DEBUG
                        return (destination, $"http://{(string.IsNullOrWhiteSpace(_configuration["DebugDestinationHost"]) ? destination : _configuration["DebugDestinationHost"])}:{port.Port}");
#else
                        return (destination, $"http://{destination}:{port.Port}");
#endif
                    }).ToDictionary(o => o.destination, o => o.Item2),
                    transforms
                );
            }
        }

        public void Reload()
        {
            var oldCts = _cts;
            _cts = new CancellationTokenSource();
            oldCts.Cancel();
        }

        private void AddOrUpdateProxy(string id, string path, IDictionary<string, string> destinationAddresses, IReadOnlyList<IReadOnlyDictionary<string, string>>? transforms)
        {
            lock (_lock)
            {
                _routes.RemoveAll(r => r.RouteId == id);
                _clusters.RemoveAll(c => c.ClusterId == id);

                var config = new RouteConfig
                {
                    RouteId = id,
                    ClusterId = id,
                    Match = new RouteMatch { Path = path },
                    Transforms = transforms,
                    AuthorizationPolicy = "HmacScheme",
                    MaxRequestBodySize = -1
                };
                config = config.WithTransformCopyRequestHeaders(true);
                _routes.Add(config);

                _clusters.Add(new ClusterConfig
                {
                    ClusterId = id,
                    LoadBalancingPolicy = "DestinationPolicy",
                    Destinations = destinationAddresses.ToDictionary(o => o.Key, o =>
                        new DestinationConfig
                        {
                            Address = o.Value
                        }
                    )
                });
            }
        }
    }
}
