namespace ChurrOS.Api.Services
{
    public interface IQueueService
    {
        Task ProduceAsync<T>(string queueName, T item, CancellationToken cancellationToken) where T : class;
        Task BatchProduceAsync<T>(string queueName, T[] item, CancellationToken cancellationToken) where T : class;
        IAsyncEnumerable<T> ConsumeAsync<T>(string queueName, string groupName, string consumerName, CancellationToken cancellationToken) where T : class;
    }
}
