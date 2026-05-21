using ChurrOS.Api.Utils;
using StackExchange.Redis;
using System.Runtime.CompilerServices;
using System.Text.Json;

namespace ChurrOS.Api.Services.Redis
{
    public class RedisStreamService : IQueueService
    {
        private readonly IConnectionMultiplexer _redis;

        public RedisStreamService(IConnectionMultiplexer redis)
        {
            _redis = redis;
        }

        public async Task ProduceAsync<T>(string queueName, T item, CancellationToken cancellationToken) where T : class
        {
            var db = _redis.GetDatabase();
            string json = JsonSerializer.Serialize(item, JsonSettings.Value);
            await db.StreamAddAsync(queueName, [new NameValueEntry("data", json)]);
        }

        public async Task BatchProduceAsync<T>(string queueName, T[] items, CancellationToken cancellationToken) where T : class
        {
            var db = _redis.GetDatabase();
            await db.StreamAddAsync(queueName, items.Select(item => new NameValueEntry("data", JsonSerializer.Serialize(item, JsonSettings.Value))).ToArray());
        }

        public async IAsyncEnumerable<T> ConsumeAsync<T>(string queueName, string groupName, string consumerName, [EnumeratorCancellation] CancellationToken cancellationToken) where T : class
        {
            var db = _redis.GetDatabase();

            if (!(await db.KeyExistsAsync(queueName)) || (await db.StreamGroupInfoAsync(queueName)).All(x => x.Name != groupName))
            {
                await db.StreamCreateConsumerGroupAsync(queueName, groupName, "0-0", true);
            }

            while (!cancellationToken.IsCancellationRequested)
            {
                var entries = await db.StreamReadGroupAsync(
                    queueName,
                    groupName,
                    consumerName,
                    ">",
                    count: 1,
                    noAck: false
                );

                foreach (var entry in entries)
                {
                    T item = JsonSerializer.Deserialize<T>((string)entry.Values.First().Value!, JsonSettings.Value)!;

                    yield return item;

                    await db.StreamAcknowledgeAsync(queueName, groupName, entry.Id);
                    await db.StreamDeleteAsync(queueName, [entry.Id]);
                }

                await Task.Delay(100, cancellationToken);
            }
        }
    }
}
