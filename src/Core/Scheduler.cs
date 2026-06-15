using Serilog;
using ILogger = Serilog.ILogger;

namespace JobService.Core;

public sealed class Scheduler : IHostedService, IDisposable
{
    private readonly BaseJob[] _jobs;
    private readonly ICronAdapter _cronAdapter;
    private readonly TimeSpan _executionTimeout;
    private readonly ILogger _logger;
    private readonly Func<DateTime> _utcNow;
    private readonly List<Task> _supervisorTasks = new();
    private readonly Dictionary<string, bool> _runningStates = new(StringComparer.Ordinal);
    private readonly object _stateLock = new();
    private CancellationTokenSource? _cts;
    private bool _started;
    private bool _stopped;

    public Scheduler(
        IEnumerable<BaseJob> jobs,
        ICronAdapter cronAdapter,
        TimeSpan executionTimeout,
        ILogger logger)
        : this(jobs, cronAdapter, executionTimeout, logger, () => DateTime.UtcNow)
    {
    }

    internal Scheduler(
        IEnumerable<BaseJob> jobs,
        ICronAdapter cronAdapter,
        TimeSpan executionTimeout,
        ILogger logger,
        Func<DateTime> utcNow)
    {
        _jobs = jobs?.ToArray() ?? throw new ArgumentNullException(nameof(jobs));
        _cronAdapter = cronAdapter ?? throw new ArgumentNullException(nameof(cronAdapter));
        _executionTimeout = executionTimeout;
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _utcNow = utcNow ?? throw new ArgumentNullException(nameof(utcNow));
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        if (_stopped)
            throw new InvalidOperationException("Scheduler has been stopped");
        if (_started)
            throw new InvalidOperationException("Scheduler already started");
        _started = true;

        var duplicates = _jobs
            .GroupBy(j => j.Name)
            .Where(g => g.Count() > 1)
            .Select(g => g.Key)
            .ToList();
        if (duplicates.Count > 0)
        {
            throw new InvalidOperationException(
                $"Duplicate job names: {string.Join(", ", duplicates)}");
        }

        var schedules = new Dictionary<string, ICronSchedule>(StringComparer.Ordinal);
        var errors = new List<string>();
        foreach (var job in _jobs)
        {
            if (!job.Enabled) continue;
            try
            {
                schedules[job.Name] = _cronAdapter.Parse(job.Schedule);
            }
            catch (Exception ex)
            {
                errors.Add($"{job.Name}: {ex.Message}");
            }
        }
        if (errors.Count > 0)
        {
            throw new InvalidOperationException(
                $"Invalid cron expressions: {string.Join("; ", errors)}");
        }

        _cts = new CancellationTokenSource();
        foreach (var job in _jobs)
        {
            if (!job.Enabled) continue;
            if (!schedules.TryGetValue(job.Name, out var schedule)) continue;
            _supervisorTasks.Add(Task.Run(() => SuperviseAsync(job, schedule, _cts.Token)));
        }
        return Task.CompletedTask;
    }

    private async Task SuperviseAsync(BaseJob job, ICronSchedule schedule, CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            var now = _utcNow();
            var next = schedule.GetNextOccurrence(now);
            var delay = next - now;
            if (delay > TimeSpan.Zero)
            {
                try
                {
                    await Task.Delay(delay, cancellationToken);
                }
                catch (OperationCanceledException)
                {
                    return;
                }
            }
            if (cancellationToken.IsCancellationRequested) return;

            lock (_stateLock)
            {
                _runningStates[job.Name] = true;
            }

            try
            {
                using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                timeoutCts.CancelAfter(_executionTimeout);
                await job.RunAsync(_logger, timeoutCts.Token);
            }
            finally
            {
                lock (_stateLock)
                {
                    _runningStates[job.Name] = false;
                }
            }
        }
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        _stopped = true;
        _cts?.Cancel();

        var deadline = _utcNow() + TimeSpan.FromSeconds(30);
        while (true)
        {
            bool anyRunning;
            lock (_stateLock)
            {
                anyRunning = _runningStates.Values.Any(v => v);
            }
            if (!anyRunning) break;
            if (_utcNow() > deadline) break;
            try
            {
                await Task.Delay(50, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }

        if (_supervisorTasks.Count > 0)
        {
            try
            {
                await Task.WhenAll(_supervisorTasks);
            }
            catch (OperationCanceledException)
            {
            }
        }
    }

    public void Dispose()
    {
        _cts?.Dispose();
    }
}
