using System.Diagnostics;
using JobService.Infrastructure.Database;
using JobService.Infrastructure.Messaging;
using JobService.Infrastructure.Redis;
using JobService.Shared.Config;
using Microsoft.Extensions.Options;

namespace JobService.Infrastructure.Health;

public sealed class DefaultHealthChecker : IHealthChecker
{
    private readonly SqlProvider _sql;
    private readonly RedisProvider _redis;
    private readonly RabbitMqProvider _rabbit;
    private readonly AppSettings _settings;

    public DefaultHealthChecker(
        SqlProvider sql,
        RedisProvider redis,
        RabbitMqProvider rabbit,
        IOptions<AppSettings> options)
    {
        _sql = sql ?? throw new ArgumentNullException(nameof(sql));
        _redis = redis ?? throw new ArgumentNullException(nameof(redis));
        _rabbit = rabbit ?? throw new ArgumentNullException(nameof(rabbit));
        _settings = options.Value;
    }

    public async Task<HealthCheckResult> CheckSqlAsync()
    {
        var sw = Stopwatch.StartNew();
        try
        {
            var ok = await _sql.PingAsync();
            sw.Stop();
            return ok
                ? new HealthCheckResult(HealthStatus.Up, sw.ElapsedMilliseconds)
                : new HealthCheckResult(HealthStatus.Down, sw.ElapsedMilliseconds, "ping returned false");
        }
        catch (Exception ex)
        {
            sw.Stop();
            return new HealthCheckResult(HealthStatus.Down, sw.ElapsedMilliseconds, ex.Message);
        }
    }

    public async Task<HealthCheckResult> CheckRedisAsync()
    {
        var sw = Stopwatch.StartNew();
        try
        {
            var ok = await _redis.PingAsync();
            sw.Stop();
            return ok
                ? new HealthCheckResult(HealthStatus.Up, sw.ElapsedMilliseconds)
                : new HealthCheckResult(HealthStatus.Down, sw.ElapsedMilliseconds, "ping returned false");
        }
        catch (Exception ex)
        {
            sw.Stop();
            return new HealthCheckResult(HealthStatus.Down, sw.ElapsedMilliseconds, ex.Message);
        }
    }

    public Task<HealthCheckResult> CheckRabbitAsync()
    {
        if (!_settings.MessagingEnabled)
            return Task.FromResult(new HealthCheckResult(HealthStatus.Disabled));
        try
        {
            var ok = _rabbit.Check();
            return Task.FromResult(ok
                ? new HealthCheckResult(HealthStatus.Up)
                : new HealthCheckResult(HealthStatus.Down, Error: "connection closed"));
        }
        catch (Exception ex)
        {
            return Task.FromResult(new HealthCheckResult(HealthStatus.Down, Error: ex.Message));
        }
    }
}
