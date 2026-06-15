using Serilog;
using ILogger = Serilog.ILogger;

namespace JobService.Core;

public sealed class JobContext
{
    public required ILogger Logger { get; init; }
}
