using System.Diagnostics;
using JobService.Core;
using JobService.Infrastructure.Health;
using JobService.Shared.Config;
using Microsoft.Extensions.Options;

namespace JobService.Jobs;

public sealed class HealthCheckJob : BaseJob
{
    public override string Name => "health-check";
    public override string Schedule => _schedule;
    public override string Description =>
        "Reports connection status with SQL Server, Redis and RabbitMQ";

    private readonly string _schedule;
    private readonly IHealthChecker _checker;

    public HealthCheckJob(IHealthChecker checker, IOptions<AppSettings> options)
    {
        _checker = checker ?? throw new ArgumentNullException(nameof(checker));
        _schedule = options.Value.HealthCheckCron;
    }

    public override async Task HandleAsync(JobContext context, CancellationToken cancellationToken)
    {
        var sw = Stopwatch.StartNew();
        var sql = _checker.CheckSqlAsync();
        var redis = _checker.CheckRedisAsync();
        var rabbit = _checker.CheckRabbitAsync();
        await Task.WhenAll(sql, redis, rabbit);
        sw.Stop();

        var sqlR = sql.Result;
        var redisR = redis.Result;
        var rabbitR = rabbit.Result;
        var allUp = sqlR.Status == HealthStatus.Up
                    && redisR.Status == HealthStatus.Up
                    && rabbitR.Status == HealthStatus.Up;

        context.Logger.Information(
            "Health check completed in {DurationMs}ms: sql={SqlStatus} redis={RedisStatus} rabbit={RabbitStatus}",
            sw.ElapsedMilliseconds,
            sqlR.Status,
            redisR.Status,
            rabbitR.Status);

        Console.WriteLine(
            $"[HealthCheck] sql={sqlR.Status} redis={redisR.Status} rabbitmq={rabbitR.Status}");
    }
}
