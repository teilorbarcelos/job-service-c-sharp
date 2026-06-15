using JobService.Shared.Errors;

namespace JobService.Shared.Config;

public sealed class AppSettings
{
    public string Environment { get; set; } = "local";
    public string LogLevel { get; set; } = "Information";
    public int ShutdownTimeoutSeconds { get; set; } = 30;
    public int JobExecutionTimeoutSeconds { get; set; } = 300;
    public string DatabaseUrl { get; set; } = string.Empty;
    public int DatabaseCommandTimeoutSeconds { get; set; } = 10;
    public string RedisHost { get; set; } = "localhost";
    public int RedisPort { get; set; } = 6379;
    public string RedisPassword { get; set; } = string.Empty;
    public int RedisDb { get; set; } = 0;
    public int RedisCommandTimeoutSeconds { get; set; } = 5;
    public bool MessagingEnabled { get; set; } = false;
    public string RabbitUrl { get; set; } = "amqp://guest:guest@localhost:5672/";
    public string RabbitUser { get; set; } = "guest";
    public string RabbitPassword { get; set; } = "guest";
    public int RabbitPublishTimeoutSeconds { get; set; } = 5;
    public string HealthCheckCron { get; set; } = "*/1 * * * *";
    public bool HealthCheckEnabled { get; set; } = true;
}

public static class EnvValidator
{
    public static string GetEnv(string key, string fallback)
        => Environment.GetEnvironmentVariable(key) ?? fallback;

    public static int GetEnvInt(string key, int fallback)
    {
        var raw = Environment.GetEnvironmentVariable(key);
        if (string.IsNullOrWhiteSpace(raw)) return fallback;
        if (!int.TryParse(raw, out var v))
            throw new ConfigurationError($"Invalid integer for {key}: '{raw}'");
        return v;
    }

    public static bool GetEnvBool(string key, bool fallback)
    {
        var raw = Environment.GetEnvironmentVariable(key);
        if (string.IsNullOrWhiteSpace(raw)) return fallback;
        if (raw.Equals("true", StringComparison.OrdinalIgnoreCase) || raw == "1")
            return true;
        if (raw.Equals("false", StringComparison.OrdinalIgnoreCase) || raw == "0")
            return false;
        throw new ConfigurationError($"Invalid boolean for {key}: '{raw}'");
    }

    public static AppSettings Load()
    {
        return new AppSettings
        {
            Environment = GetEnv("ENVIRONMENT", "local"),
            LogLevel = GetEnv("LOG_LEVEL", "Information"),
            ShutdownTimeoutSeconds = GetEnvInt("SHUTDOWN_TIMEOUT_SECONDS", 30),
            JobExecutionTimeoutSeconds = GetEnvInt("JOB_EXECUTION_TIMEOUT_SECONDS", 300),
            DatabaseUrl = GetEnv("DATABASE_URL", string.Empty),
            DatabaseCommandTimeoutSeconds = GetEnvInt("DATABASE_COMMAND_TIMEOUT_SECONDS", 10),
            RedisHost = GetEnv("REDIS_HOST", "localhost"),
            RedisPort = GetEnvInt("REDIS_PORT", 6379),
            RedisPassword = GetEnv("REDIS_PASSWORD", string.Empty),
            RedisDb = GetEnvInt("REDIS_DB", 0),
            RedisCommandTimeoutSeconds = GetEnvInt("REDIS_COMMAND_TIMEOUT_SECONDS", 5),
            MessagingEnabled = GetEnvBool("MESSAGING_ENABLED", false),
            RabbitUrl = GetEnv("RABBIT_URL", "amqp://guest:guest@localhost:5672/"),
            RabbitUser = GetEnv("RABBIT_USER", "guest"),
            RabbitPassword = GetEnv("RABBIT_PASSWORD", "guest"),
            RabbitPublishTimeoutSeconds = GetEnvInt("RABBITMQ_PUBLISH_TIMEOUT", 5),
            HealthCheckCron = GetEnv("HEALTH_CHECK_CRON", "*/1 * * * *"),
            HealthCheckEnabled = GetEnvBool("HEALTH_CHECK_ENABLED", true),
        };
    }
}
