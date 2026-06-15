using Serilog;
using Serilog.Core;
using Serilog.Events;

namespace JobService.Shared.Utils;

public static class AppLogger
{
    public static Logger Create(string level = "Information", string? environment = null)
    {
        LogEventLevel parsed;
        if (!Enum.TryParse(level, ignoreCase: true, out parsed))
            parsed = LogEventLevel.Information;

        var config = new LoggerConfiguration()
            .MinimumLevel.Is(parsed)
            .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
            .MinimumLevel.Override("Microsoft.Hosting.Lifetime", LogEventLevel.Information)
            .MinimumLevel.Override("System", LogEventLevel.Warning)
            .Enrich.FromLogContext()
            .WriteTo.Console(outputTemplate:
                "[{Timestamp:yyyy-MM-dd HH:mm:ss} {Level:u3}] {Message:lj}{NewLine}{Exception}");

        if (!string.IsNullOrEmpty(environment))
            config = config.Enrich.WithProperty("Environment", environment);

        return config.CreateLogger();
    }
}
