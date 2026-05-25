using ChurrOS.Api.Models.Dtos.Application;
using DispatchR.Abstractions.Send;

namespace ChurrOS.Api.Commands.Applications
{
    /// <summary>
    /// Recomputes the Application Size recommendation from recent CPU/memory usage.
    /// Scope is optional: an application, an environment, or — when neither is set —
    /// every application in the current tenant (used by the nightly job).
    /// </summary>
    public class AnalyzeApplicationUsage : IRequest<AnalyzeApplicationUsage, ValueTask<AnalyzeUsageResultItem>>
    {
        /// <summary>When set, only this application is analyzed.</summary>
        public string? ApplicationName { get; private set; }

        /// <summary>When set, every application in this environment is analyzed.</summary>
        public string? EnvironmentName { get; private set; }

        /// <summary>
        /// Set by trusted callers (the nightly job) to bypass the manager permission
        /// check. HTTP callers always leave this false.
        /// </summary>
        public bool SkipAuthorization { get; private set; }

        public AnalyzeApplicationUsage(string? applicationName = null, string? environmentName = null, bool skipAuthorization = false)
        {
            ApplicationName = applicationName;
            EnvironmentName = environmentName;
            SkipAuthorization = skipAuthorization;
        }
    }
}
