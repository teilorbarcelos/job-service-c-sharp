namespace JobService.Core;

public sealed record JobResult(
    string Job,
    JobStatus Status,
    long DurationMs,
    string? Error = null);
