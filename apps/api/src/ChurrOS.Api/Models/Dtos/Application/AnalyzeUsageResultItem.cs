namespace ChurrOS.Api.Models.Dtos.Application
{
    /// <summary>
    /// Summary returned after an on-demand or scheduled usage analysis run.
    /// </summary>
    public class AnalyzeUsageResultItem
    {
        /// <summary>Number of applications that were analyzed.</summary>
        public int ApplicationsAnalyzed { get; set; }

        /// <summary>Number of applications for which a different size is recommended.</summary>
        public int RecommendationsCount { get; set; }
    }
}
