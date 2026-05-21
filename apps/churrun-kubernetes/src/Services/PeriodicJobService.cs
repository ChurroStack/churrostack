namespace ChurrunKubernetes.Services
{
    public abstract class PeriodicJobService<T> : BackgroundService
    {
        private readonly TimeSpan _periodicity;
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ILogger<T> _logger;
        protected ILogger<T> Logger => _logger;

        public PeriodicJobService(TimeSpan periodicity, IServiceScopeFactory scopeFactory, ILogger<T> logger)
        {
            _periodicity = periodicity;
            _scopeFactory = scopeFactory;
            _logger = logger;
        }

        protected abstract Task DoWorkAsync(IServiceProvider serviceProvider, CancellationToken stoppingToken);

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            var timer = new PeriodicTimer(_periodicity);

            while (await timer.WaitForNextTickAsync(stoppingToken))
            {
                try
                {
                    using var scope = _scopeFactory.CreateScope();
                    await DoWorkAsync(scope.ServiceProvider, stoppingToken);
                }
                catch (OperationCanceledException)
                {
                    // graceful shutdown
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error running periodic job");
                }
            }
        }
    }
}
