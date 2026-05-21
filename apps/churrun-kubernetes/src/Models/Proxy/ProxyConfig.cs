using Microsoft.Extensions.Primitives;
using Yarp.ReverseProxy.Configuration;

namespace ChurrunKubernetes.Models.Proxy
{
    public class ProxyConfig : IProxyConfig
    {
        public IReadOnlyList<RouteConfig> Routes { get; }
        public IReadOnlyList<ClusterConfig> Clusters { get; }
        public IChangeToken ChangeToken { get; }

        public ProxyConfig(
            IReadOnlyList<RouteConfig> routes,
            IReadOnlyList<ClusterConfig> clusters,
            IChangeToken changeToken)
        {
            Routes = routes;
            Clusters = clusters;
            ChangeToken = changeToken;
        }
    }
}