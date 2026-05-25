using ChurrOS.Api.Models.Dtos.Application;
using DispatchR.Abstractions.Send;

namespace ChurrOS.Api.Commands.Applications
{
    /// <summary>
    /// Returns the stored size recommendation for an application (used by the banner).
    /// </summary>
    public class GetApplicationSizeRecommendation : IRequest<GetApplicationSizeRecommendation, ValueTask<ApplicationSizeRecommendationItem>>
    {
        public string AppName { get; private set; }

        public GetApplicationSizeRecommendation(string appName)
        {
            AppName = appName;
        }
    }
}
