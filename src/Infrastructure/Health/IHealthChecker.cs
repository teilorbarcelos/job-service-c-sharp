namespace JobService.Infrastructure.Health;

public enum HealthStatus
{
    Up,
    Down,
    Disabled
}

public sealed record HealthCheckResult(HealthStatus Status, long? LatencyMs = null, string? Error = null);

public interface IHealthChecker
{
    Task<HealthCheckResult> CheckSqlAsync();
    Task<HealthCheckResult> CheckRedisAsync();
    Task<HealthCheckResult> CheckRabbitAsync();
}
