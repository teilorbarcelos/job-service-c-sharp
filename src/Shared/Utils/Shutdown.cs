using Microsoft.Extensions.Hosting;
using Serilog.Core;

namespace JobService.Shared.Utils;

public sealed class ShutdownHandler
{
    private readonly Logger _logger;
    private readonly IHostApplicationLifetime _lifetime;
    private readonly TimeSpan _timeout;
    private readonly Func<Task> _cleanup;

    public ShutdownHandler(Logger logger, IHostApplicationLifetime lifetime, TimeSpan timeout, Func<Task> cleanup)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _lifetime = lifetime ?? throw new ArgumentNullException(nameof(lifetime));
        _timeout = timeout;
        _cleanup = cleanup ?? throw new ArgumentNullException(nameof(cleanup));
    }

    public void Register()
    {
        AppDomain.CurrentDomain.ProcessExit += (_, _) => HandleAsync("ProcessExit").GetAwaiter().GetResult();
        _lifetime.ApplicationStopping.Register(() => HandleAsync("ApplicationStopping").GetAwaiter().GetResult());
    }

    private async Task HandleAsync(string source)
    {
        _logger.Information("Shutdown requested via {Source}, draining...", source);
        try
        {
            using var cts = new CancellationTokenSource(_timeout);
            await _cleanup().WaitAsync(cts.Token);
            _logger.Information("Cleanup completed within {Timeout}s", _timeout.TotalSeconds);
        }
        catch (OperationCanceledException)
        {
            _logger.Warning("Cleanup exceeded timeout of {Timeout}s", _timeout.TotalSeconds);
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Cleanup failed");
        }
    }
}
