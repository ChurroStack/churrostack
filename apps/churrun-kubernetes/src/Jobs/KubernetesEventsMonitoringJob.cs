using ChurrunKubernetes.Models.Logs;
using ChurrunKubernetes.Services;
using ChurrunKubernetes.Services.State;

namespace ChurrunKubernetes.Jobs
{
    public class KubernetesEventsMonitoringJob : BackgroundService
    {
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly IConfiguration _configuration;
        private readonly ILogger<KubernetesEventsMonitoringJob> _logger;
        private readonly EventsStateService<KubernetesGenericEvent> _eventsStateService;

        public KubernetesEventsMonitoringJob(IServiceScopeFactory scopeFactory, IConfiguration configuration, ILogger<KubernetesEventsMonitoringJob> logger, EventsStateService<KubernetesGenericEvent> eventsStateService)
        {
            _scopeFactory = scopeFactory;
            _configuration = configuration;
            _logger = logger;
            _eventsStateService = eventsStateService;
        }

        protected override async Task ExecuteAsync(CancellationToken cancellationToken)
        {
            using var scope = _scopeFactory.CreateScope();
            var kube = scope.ServiceProvider.GetService<KubernetesService>()!;

            do
            {
                try
                {
                    await foreach (var item in kube.MonitorEventsAsync(_configuration["Kubernetes:Namespace"]!, CancellationToken.None))
                    {
                        _eventsStateService.AddEvent(item);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error monitoring Kubernetes events");
                    await Task.Delay(TimeSpan.FromSeconds(5));
                }
            } while (!cancellationToken.IsCancellationRequested);
        }
    }
}
