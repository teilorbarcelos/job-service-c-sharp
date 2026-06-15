using System.Diagnostics;
using Serilog.Core;

namespace JobService.Core;

public abstract class BaseJob
{
    public abstract string Name { get; }
    public abstract string Schedule { get; }
    public virtual string Description => string.Empty;
    public bool Enabled { get; set; } = true;

    public abstract Task HandleAsync(JobContext context, CancellationToken cancellationToken);

    public async Task<JobResult> RunAsync(Logger logger, CancellationToken cancellationToken)
    {
        var sw = Stopwatch.StartNew();
        var context = new JobContext { Logger = logger };
        try
        {
            await HandleAsync(context, cancellationToken);
            sw.Stop();
            logger.Information("Job {JobName} completed in {DurationMs}ms", Name, sw.ElapsedMilliseconds);
            return new JobResult(Name, JobStatus.Success, sw.ElapsedMilliseconds);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            sw.Stop();
            logger.Warning("Job {JobName} cancelled after {DurationMs}ms", Name, sw.ElapsedMilliseconds);
            return new JobResult(Name, JobStatus.Cancelled, sw.ElapsedMilliseconds);
        }
        catch (Exception ex)
        {
            sw.Stop();
            logger.Error(ex, "Job {JobName} failed after {DurationMs}ms", Name, sw.ElapsedMilliseconds);
            return new JobResult(Name, JobStatus.Failed, sw.ElapsedMilliseconds, ex.Message);
        }
    }
}
