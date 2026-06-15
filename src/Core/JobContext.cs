using Serilog.Core;

namespace JobService.Core;

public sealed class JobContext
{
    public required Logger Logger { get; init; }
}
