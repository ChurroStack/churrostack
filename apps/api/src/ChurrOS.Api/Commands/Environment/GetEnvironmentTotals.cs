using ChurrOS.Api.Models.Dtos.Environment;
using DispatchR.Abstractions.Send;

namespace ChurrOS.Api.Commands.Environment
{
    /// <summary>
    /// Real-time CPU/memory totals for the environment header: observed usage,
    /// committed (sum of all apps' configured size), and the quota ceiling.
    /// </summary>
    public class GetEnvironmentTotals : IRequest<GetEnvironmentTotals, ValueTask<EnvironmentTotalsItem>>
    {
        public string EnvironmentName { get; private set; }

        public GetEnvironmentTotals(string environmentName)
        {
            EnvironmentName = environmentName;
        }
    }
}
