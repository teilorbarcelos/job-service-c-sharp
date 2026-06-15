namespace JobService.Infrastructure.Database;

public interface ISqlProvider : IDisposable
{
    Task<Microsoft.Data.SqlClient.SqlConnection> OpenAsync(CancellationToken cancellationToken = default);
    Task<bool> PingAsync(CancellationToken cancellationToken = default);
}
