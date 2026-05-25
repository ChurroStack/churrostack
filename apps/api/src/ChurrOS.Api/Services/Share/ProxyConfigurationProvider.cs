using ChurrOS.Api.Data;
using ChurrOS.Api.Models.Dtos.Share;
using ChurrOS.Api.Models.Dtos.Template.Definition;
using ChurrOS.Api.Services;
using ChurrOS.Api.Services.AutoStart;
using ChurrOS.Api.Utils;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Primitives;
using Npgsql;
using System.Data;
using System.Text.Json;
using Yarp.ReverseProxy.Configuration;
using Yarp.ReverseProxy.Transforms;

namespace ChurrOS.Api.Services.Share
{
    public class ProxyConfigurationProvider : IProxyConfigProvider
    {
        private readonly object _lock = new();

        private List<RouteConfig> _routes = new();
        private List<ClusterConfig> _clusters = new();

        private CancellationTokenSource _cts = new();

        private readonly IServiceScopeFactory _serviceScopeFactory;
        private readonly ICacheService _cacheService;

        public ProxyConfigurationProvider(IServiceScopeFactory serviceScopeFactory, ICacheService cacheService)
        {
            _serviceScopeFactory = serviceScopeFactory;
            _cacheService = cacheService;
        }

        public IProxyConfig GetConfig()
        {
            return new ProxyConfig(_routes, _clusters, new CancellationChangeToken(_cts.Token));
        }

        public async Task Initialize()
        {
            using var scope = _serviceScopeFactory.CreateScope();

            var dbContext = scope.ServiceProvider.GetRequiredService<ChurrosDbContext>();

            var conn = (NpgsqlConnection)dbContext.Database.GetDbConnection();
            var isOpen = conn.State == System.Data.ConnectionState.Open;
            if (!isOpen)
            {
                await conn.OpenAsync();
            }

            try
            {
                var environments = new DataTable();
                environments.Columns.Add("name", typeof(string));
                environments.Columns.Add("uri", typeof(string));
                environments.Columns.Add("host", typeof(Array));
                environments.Columns.Add("port", typeof(int));

                await using (var envCmd = new NpgsqlCommand("""
                SELECT
                    e.name,
                    e.host,
                    e.port
                FROM cs.environment e
                """, conn))

                await using (var envReader = await envCmd.ExecuteReaderAsync())
                {
                    environments.Load(envReader);
                }

                var apps = new DataTable();
                apps.Columns.Add("name", typeof(string));
                apps.Columns.Add("ports", typeof(string));
                apps.Columns.Add("environment", typeof(string));

                await using (var appCmd = new NpgsqlCommand("""
                SELECT
                    a.name,
                    a.ports,
                    e.name AS environment
                FROM cs.application a
                JOIN cs.environment e
                    ON a.environment_id = e.id;
                """, conn))

                await using (var appReader = await appCmd.ExecuteReaderAsync())
                {
                    apps.Load(appReader);
                }

                foreach (DataRow env in environments.Rows)
                {
                    AddEnvironment((string)env["name"], ((string[])env["host"])[1], (int)env["port"]);
                }

                foreach (DataRow deployment in apps.Rows)
                {
                    var ports = JsonSerializer.Deserialize<PortDefinition[]>((string)deployment["ports"], JsonSettings.Value);
                    if (ports is not null && ports.Any())
                    {
                        AddApplication((string)deployment["name"], ports, (string)deployment["environment"]);
                    }
                }

                Reload();
            }
            finally
            {
                if (!isOpen)
                {
                    await conn.CloseAsync();
                }
            }
        }

        public void AddEnvironment(string environmentName, string host, int port)
        {
            // TODO: X-Port and HMAC Signing

            lock (_lock)
            {
                _clusters.RemoveAll(c => c.ClusterId == environmentName);
                _clusters.Add(new ClusterConfig
                {
                    ClusterId = environmentName,
                    Destinations = new Dictionary<string, DestinationConfig>
                    {
                        ["runner"] = new DestinationConfig
                        {
                            Address = host
                        }
                    }
                });
            }
        }

        public void RemoveApplication(string deploymentName)
        {
            lock (_lock)
            {
                _routes.RemoveAll(r => r.RouteId.StartsWith($"{deploymentName}:"));
                Reload();
            }
            _ = _cacheService.InvalidateAsync(AutoStartConstants.RouteCacheKey(deploymentName));
        }

        public void RemoveEnvironment(string environmentName)
        {
            lock (_lock)
            {
                _clusters.RemoveAll(c => c.ClusterId == environmentName);
                _routes.RemoveAll(r => r.ClusterId == environmentName);
                Reload();
            }
        }

        public void AddApplication(string appName, PortDefinition[] deploymentPorts, string environmentName)
        {
            _ = _cacheService.InvalidateAsync(AutoStartConstants.RouteCacheKey(appName));
            lock (_lock)
            {
                _routes.RemoveAll(r => r.RouteId.StartsWith($"{appName}:"));
                foreach (var port in deploymentPorts)
                {
                    string? authorizationPolicy;
                    switch (port.Authentication)
                    {
                        default:
                        case AuthenticationMode.Oidc:
                            authorizationPolicy = "AppCookiePolicy";
                            break;
                        case AuthenticationMode.Jwt:
                        case AuthenticationMode.JwtDcr:
                            authorizationPolicy = "AppJwtPolicy";
                            break;
                        case AuthenticationMode.Anonymous:
                            authorizationPolicy = null;
                            break;
                    }
                    var config = new RouteConfig
                    {
                        RouteId = $"{appName}:{port.Name}",
                        ClusterId = environmentName,
                        Match = new RouteMatch { Path = $"/share/{appName}/{port.Name}/{{**catch-all}}" },
                        AuthorizationPolicy = authorizationPolicy,
                        MaxRequestBodySize = -1
                    };
                    config = config.WithTransformCopyRequestHeaders();
                    _routes.Add(config);
                }
            }
        }

        public void Reload()
        {
            var oldCts = _cts;
            _cts = new CancellationTokenSource();
            oldCts.Cancel();
        }
    }
}
