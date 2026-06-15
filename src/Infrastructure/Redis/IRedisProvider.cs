namespace JobService.Infrastructure.Redis;

public interface IRedisProvider : IDisposable
{
    StackExchange.Redis.IDatabase GetDatabase();
    Task<bool> PingAsync();
}
