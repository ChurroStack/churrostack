using ChurrOS.Api.Utils.Exceptions;
using StackExchange.Redis;

namespace ChurrOS.Api.Services
{
    public class QuotaService
    {
        public enum QuotaType
        {
            Network
        }

        private readonly IConnectionMultiplexer _connectionMultiplexer;
        private readonly ITenantResolver _tenantResolver;

        private static LuaScript IncreaseScript => LuaScript.Prepare("""
            local increment = tonumber(@increment) or 1
            local limit = tonumber(redis.call('HGET', @key, 'max') or '0')
            local current = tonumber(redis.call('HGET', @key, 'used') or '0')

            if current > limit then
                return {err='QuotaExceeded'}
            else
                redis.call('HINCRBYFLOAT', @key, 'used', increment)
                return current + increment
            end
            """);

        private static LuaScript DecreaseScript => LuaScript.Prepare("""
            local decrement = tonumber(@decrement) or -1
            local current = tonumber(redis.call('HGET', @key, 'used') or '0')

            if current < decrement * -1 then
                redis.call('HSET', @key, 'used', 0)
            else
                redis.call('HINCRBYFLOAT', @key, 'used', decrement)
                return current + decrement
            end
            """);

        private static LuaScript CheckScript => LuaScript.Prepare("""
            local limit = tonumber(redis.call('HGET', @key, 'max') or '0')
            local current = tonumber(redis.call('HGET', @key, 'used') or '0')
            if current < limit then
                return 1
            else
                return 0
            end
            """);

        public QuotaService(IConnectionMultiplexer connectionMultiplexer, ITenantResolver tenantResolver)
        {
            _connectionMultiplexer = connectionMultiplexer;
            _tenantResolver = tenantResolver;
        }

        public async Task InitializeAsync(long networkQuota)
        {
            var accountId = _tenantResolver.AccountId;
            var db = _connectionMultiplexer.GetDatabase();
            await db.HashSetAsync($"churros_tenant:{accountId}:quota:network", new[]
            {
                new HashEntry("used", 0),
                new HashEntry("max", networkQuota)
            });
        }

        public async Task EnsureHasQuotaAsync(QuotaType quota)
        {
            string keyName;
            switch (quota)
            {
                default:
                    throw new NotImplementedException();
                case QuotaType.Network:
                    keyName = "network";
                    break;
            }
            var db = _connectionMultiplexer.GetDatabase();
            var hasQuota = ((int)await db.ScriptEvaluateAsync(CheckScript, new { key = (RedisKey)$"churros_tenant:{_tenantResolver.AccountId}:quota:{keyName}" })) == 1;
            if (hasQuota)
                return;
            switch (quota)
            {
                default:
                    throw new NotImplementedException();
                case QuotaType.Network:
                    throw new HttpException(429, "You exceeded your current data transfer quota, please check your plan and billing details to increase your quota.");
            }
        }

        public async Task IncrementUsageAsync(QuotaType type, double increment)
        {
            string keyName;
            switch (type)
            {
                default:
                    throw new NotImplementedException();
                case QuotaType.Network:
                    keyName = "network";
                    break;
            }
            var db = _connectionMultiplexer.GetDatabase();

            try
            {
                await db.ScriptEvaluateAsync(IncreaseScript, new { key = (RedisKey)$"churros_tenant:{_tenantResolver.AccountId}:quota:{keyName}", increment });
            }
            catch (RedisServerException ex) when (ex.Message.Contains("QuotaExceeded"))
            {
                // Do not notify
            }
        }

        public async Task DecrementUsageAsync(QuotaType type, double decrement)
        {
            if (decrement < 0)
                throw new ArgumentException("Decrement must be a positive number");
            decrement = -1 * decrement;
            string keyName;
            switch (type)
            {
                default:
                    throw new NotImplementedException();
                case QuotaType.Network:
                    keyName = "network";
                    break;
            }
            var db = _connectionMultiplexer.GetDatabase();
            await db.ScriptEvaluateAsync(DecreaseScript, new { key = (RedisKey)$"churros_tenant:{_tenantResolver.AccountId}:quota:{keyName}", decrement });
        }

        public async Task<(QuotaType Type, double total, double used)[]> GetTenantQuotaAsync()
        {
            var result = new List<(QuotaType Type, double total, double used)>();
            var db = _connectionMultiplexer.GetDatabase();
            var network = await db.HashGetAllAsync($"churros_tenant:{_tenantResolver.AccountId}:quota:network");
            if (network != null && network.Any())
            {
                result.Add((QuotaType.Network, (long?)network.FirstOrDefault(o => o.Name == "max").Value ?? 0, (long?)network.First(o => o.Name == "used").Value ?? 0));
            }
            return result.ToArray();
        }

        public async Task SetUsageAsync(QuotaType type, double value)
        {
            string keyName;
            switch (type)
            {
                default:
                    throw new NotImplementedException();
                case QuotaType.Network:
                    keyName = "network";
                    break;
            }
            var accountId = _tenantResolver.AccountId;
            var db = _connectionMultiplexer.GetDatabase();
            await db.HashSetAsync($"churros_tenant:{accountId}:quota:{keyName}", new[]
            {
                new HashEntry("used", value)
            });
        }
    }
}
