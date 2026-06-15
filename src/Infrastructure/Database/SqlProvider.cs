using System.Data;
using JobService.Shared.Config;
using JobService.Shared.Errors;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Options;

namespace JobService.Infrastructure.Database;

public class SqlProvider : ISqlProvider
{
    private readonly string _connectionString;
    private readonly int _commandTimeoutSeconds;
    private readonly ILogger<SqlProvider> _logger;
    private bool _disposed;

    public SqlProvider(IOptions<AppSettings> options, ILogger<SqlProvider> logger)
    {
        ArgumentNullException.ThrowIfNull(options);
        _connectionString = options.Value.DatabaseUrl;
        if (string.IsNullOrWhiteSpace(_connectionString))
            throw new ConfigurationError("DATABASE_URL is not configured");
        _commandTimeoutSeconds = options.Value.DatabaseCommandTimeoutSeconds;
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    protected virtual SqlConnection CreateConnection() => new SqlConnection(_connectionString);

    public virtual async Task<SqlConnection> OpenAsync(CancellationToken cancellationToken = default)
    {
        if (_disposed) throw new ObjectDisposedException(nameof(SqlProvider));
        var conn = CreateConnection();
        try
        {
            await conn.OpenAsync(cancellationToken);
            return conn;
        }
        catch
        {
            await conn.DisposeAsync();
            throw new ConnectionError("SQL Server", "Failed to open connection");
        }
    }

    public virtual async Task<bool> PingAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            await using var conn = await OpenAsync(cancellationToken);
            await using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT 1";
            cmd.CommandTimeout = _commandTimeoutSeconds;
            var result = await cmd.ExecuteScalarAsync(cancellationToken);
            return result is int i && i == 1;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "SQL Server ping failed");
            return false;
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
    }
}
