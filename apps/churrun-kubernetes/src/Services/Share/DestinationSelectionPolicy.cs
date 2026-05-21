using Yarp.ReverseProxy.LoadBalancing;
using Yarp.ReverseProxy.Model;

namespace ChurrunKubernetes.Services.Share
{
    public class DestinationSelectionPolicy : ILoadBalancingPolicy
    {
        public string Name => "DestinationPolicy";

        public DestinationState? PickDestination(HttpContext context, ClusterState cluster, IReadOnlyList<DestinationState> availableDestinations)
        {
            if (context.Request.Headers.TryGetValue("X-Destination-Id", out var destinationId))
            {
                var destination = availableDestinations.FirstOrDefault(d => d.DestinationId.Equals(destinationId.ToString(), StringComparison.InvariantCultureIgnoreCase));
                if (destination != null)
                {
                    return destination;
                }
            }
            return availableDestinations[0];
        }
    }
}
