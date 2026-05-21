using ChurrunKubernetes.Models.Logs;
using ChurrunKubernetes.Services;
using ChurrunKubernetes.Services.State;

namespace ChurrunKubernetes.Jobs
{
    public class KubernetesDeploymentsMonitoringJob : BackgroundService
    {
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly IConfiguration _configuration;
        private readonly ILogger<KubernetesDeploymentsMonitoringJob> _logger;
        private readonly EventsStateService<KubernetesDeploymentEvent> _stateChangesService;

        public KubernetesDeploymentsMonitoringJob(IServiceScopeFactory scopeFactory, IConfiguration configuration, ILogger<KubernetesDeploymentsMonitoringJob> logger, EventsStateService<KubernetesDeploymentEvent> stateChangesService)
        {
            _scopeFactory = scopeFactory;
            _configuration = configuration;
            _logger = logger;
            _stateChangesService = stateChangesService;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            using var scope = _scopeFactory.CreateScope();
            var kube = scope.ServiceProvider.GetService<KubernetesService>()!;
            do
            {
                try
                {
                    await foreach (var item in kube.MonitorDeploymentAsync(_configuration["Kubernetes:Namespace"]!, CancellationToken.None))
                    {
                        _stateChangesService.AddEvent(item);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error monitoring Kubernetes events");
                    await Task.Delay(TimeSpan.FromSeconds(5));
                }
            } while (!stoppingToken.IsCancellationRequested);
        }
    }
}
