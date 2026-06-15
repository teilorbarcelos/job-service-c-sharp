using JobService.Shared.Config;
using Microsoft.Extensions.Options;
using StackExchange.Redis;

namespace JobService.Infrastructure.Redis;

public sealed class RedisProvider : IDisposable
{
    private readonly Lazy<ConnectionMultiplexer> _connection;
    private readonly ILogger<RedisProvider> _logger;
    private bool _disposed;

    public RedisProvider(IOptions<AppSettings> options, ILogger<RedisProvider> logger)
    {
        ArgumentNullException.ThrowIfNull(options);
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _connection = new Lazy<ConnectionMultiplexer>(() =>
        {
            var cfg = options.Value;
            var config = BuildConfiguration(cfg);
            return ConnectionMultiplexer.Connect(config);
        });
    }

    private static ConfigurationOptions BuildConfiguration(AppSettings cfg)
    {
        if (!string.IsNullOrEmpty(cfg.RedisHost) &&
            (cfg.RedisHost.StartsWith("redis://", StringComparison.OrdinalIgnoreCase) ||
             cfg.RedisHost.StartsWith("rediss://", StringComparison.OrdinalIgnoreCase)))
        {
            return ConfigurationOptions.Parse(cfg.RedisHost);
        }

        var options = new ConfigurationOptions
        {
            EndPoints = { { cfg.RedisHost, cfg.RedisPort } },
            AbortOnConnectFail = false,
            ConnectTimeout = 5000,
            SyncTimeout = cfg.RedisCommandTimeoutSeconds * 1000,
        };
        if (!string.IsNullOrEmpty(cfg.RedisPassword))
            options.Password = cfg.RedisPassword;
        if (cfg.RedisDb > 0)
            options.DefaultDatabase = cfg.RedisDb;
        return options;
    }

    public IDatabase GetDatabase() => _connection.Value.GetDatabase();

    public async Task<bool> PingAsync()
    {
        try
        {
            var db = GetDatabase();
            var pong = await db.PingAsync();
            return pong > TimeSpan.Zero;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Redis ping failed");
            return false;
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        if (_connection.IsValueCreated)
        {
            _connection.Value.Dispose();
        }
    }
}
