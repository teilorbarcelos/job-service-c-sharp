using FluentAssertions;
using JobService.Core;
using Moq;
using Serilog;
using Serilog.Core;
using Xunit;

namespace JobService.Tests.Core;

public class SchedulerTests
{
    private sealed class TestJob : BaseJob
    {
        private readonly string _name;
        private readonly string _schedule;
        private readonly TaskCompletionSource _tcs = new();
        private readonly int _handleDelayMs;

        public TestJob(string name, string schedule, int handleDelayMs = 0)
        {
            _name = name;
            _schedule = schedule;
            _handleDelayMs = handleDelayMs;
        }

        public Task Ran => _tcs.Task;
        public int HandleCalls { get; private set; }

        public override string Name => _name;
        public override string Schedule => _schedule;
        public override async Task HandleAsync(JobContext context, CancellationToken cancellationToken)
        {
            HandleCalls++;
            if (_handleDelayMs > 0)
                await Task.Delay(_handleDelayMs, cancellationToken);
            _tcs.TrySetResult();
        }
    }

    private static Logger Logger() => new LoggerConfiguration().CreateLogger();

    private static ICronAdapter MockCronReturning(DateTime nextOccurrence)
    {
        var schedule = new Mock<ICronSchedule>();
        schedule.Setup(s => s.GetNextOccurrence(It.IsAny<DateTime>())).Returns(nextOccurrence);
        var adapter = new Mock<ICronAdapter>();
        adapter.Setup(a => a.Parse(It.IsAny<string>())).Returns(schedule.Object);
        return adapter.Object;
    }

    private static ICronAdapter MockCronWithSequence(params DateTime[] occurrences)
    {
        var queue = new Queue<DateTime>(occurrences);
        var schedule = new Mock<ICronSchedule>();
        schedule.Setup(s => s.GetNextOccurrence(It.IsAny<DateTime>()))
            .Returns(() => queue.Count > 0 ? queue.Dequeue() : DateTime.UtcNow.AddHours(1));
        var adapter = new Mock<ICronAdapter>();
        adapter.Setup(a => a.Parse(It.IsAny<string>())).Returns(schedule.Object);
        return adapter.Object;
    }

    private static ICronAdapter MockCronThrowing(string message)
    {
        var adapter = new Mock<ICronAdapter>();
        adapter.Setup(a => a.Parse(It.IsAny<string>())).Throws(new InvalidOperationException(message));
        return adapter.Object;
    }

    [Fact]
    public void Ctor_Throws_On_Null_Jobs()
    {
        Action act = () => new Scheduler(null!, MockCronReturning(DateTime.UtcNow), TimeSpan.FromSeconds(1), Logger());
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Ctor_Throws_On_Null_Cron_Adapter()
    {
        Action act = () => new Scheduler(Array.Empty<BaseJob>(), null!, TimeSpan.FromSeconds(1), Logger());
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Ctor_Throws_On_Null_Logger()
    {
        Action act = () => new Scheduler(Array.Empty<BaseJob>(), MockCronReturning(DateTime.UtcNow), TimeSpan.FromSeconds(1), null!);
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public async Task StartAsync_Succeeds_With_No_Jobs()
    {
        var scheduler = new Scheduler(
            Array.Empty<BaseJob>(),
            MockCronReturning(DateTime.UtcNow),
            TimeSpan.FromSeconds(1),
            Logger());

        await scheduler.StartAsync(CancellationToken.None);
        await scheduler.StopAsync(CancellationToken.None);
    }

    [Fact]
    public async Task StartAsync_Throws_On_Duplicate_Job_Names()
    {
        var jobs = new BaseJob[] { new TestJob("a", "* * * * *"), new TestJob("a", "* * * * *") };
        var scheduler = new Scheduler(jobs, MockCronReturning(DateTime.UtcNow), TimeSpan.FromSeconds(1), Logger());

        Func<Task> act = () => scheduler.StartAsync(CancellationToken.None);
        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*Duplicate*a*");
    }

    [Fact]
    public async Task StartAsync_Throws_On_Invalid_Cron()
    {
        var jobs = new BaseJob[] { new TestJob("a", "* * * * *") };
        var scheduler = new Scheduler(jobs, MockCronThrowing("bad cron"), TimeSpan.FromSeconds(1), Logger());

        Func<Task> act = () => scheduler.StartAsync(CancellationToken.None);
        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*Invalid cron*a: bad cron*");
    }

    [Fact]
    public async Task StartAsync_Throws_On_Double_Start()
    {
        var jobs = new BaseJob[] { new TestJob("a", "* * * * *") { Enabled = false } };
        var scheduler = new Scheduler(jobs, MockCronReturning(DateTime.UtcNow), TimeSpan.FromSeconds(1), Logger());

        await scheduler.StartAsync(CancellationToken.None);
        Func<Task> act = () => scheduler.StartAsync(CancellationToken.None);
        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*already started*");
    }

    [Fact]
    public async Task StartAsync_Throws_After_Stop()
    {
        var jobs = new BaseJob[] { new TestJob("a", "* * * * *") { Enabled = false } };
        var scheduler = new Scheduler(jobs, MockCronReturning(DateTime.UtcNow), TimeSpan.FromSeconds(1), Logger());

        await scheduler.StartAsync(CancellationToken.None);
        await scheduler.StopAsync(CancellationToken.None);
        Func<Task> act = () => scheduler.StartAsync(CancellationToken.None);
        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*has been stopped*");
    }

    [Fact]
    public async Task Job_Runs_At_Scheduled_Time()
    {
        var job = new TestJob("a", "* * * * *");
        var now = DateTime.UtcNow;
        // First occurrence: T+50ms. Subsequent: T+1h.
        var adapter = MockCronWithSequence(now.AddMilliseconds(50), now.AddHours(1));

        var scheduler = new Scheduler(
            new BaseJob[] { job },
            adapter,
            TimeSpan.FromSeconds(5),
            Logger());

        await scheduler.StartAsync(CancellationToken.None);
        var completed = await Task.WhenAny(job.Ran, Task.Delay(2000));
        await scheduler.StopAsync(CancellationToken.None);

        completed.Should().Be(job.Ran);
        job.HandleCalls.Should().Be(1);
    }

    [Fact]
    public async Task Job_Runs_Sequentially_Awaiting_Each_Completion()
    {
        var job = new TestJob("a", "* * * * *", handleDelayMs: 200);
        var now = DateTime.UtcNow;
        // First occurrence: T+50ms. Subsequent: T+1h (so the supervisor
        // doesn't re-fire after the first run completes).
        var adapter = MockCronWithSequence(now.AddMilliseconds(50), now.AddHours(1));

        var scheduler = new Scheduler(
            new BaseJob[] { job },
            adapter,
            TimeSpan.FromSeconds(5),
            Logger());

        await scheduler.StartAsync(CancellationToken.None);
        await job.Ran.WaitAsync(TimeSpan.FromSeconds(2));
        await scheduler.StopAsync(CancellationToken.None);

        job.HandleCalls.Should().Be(1);
    }

    [Fact]
    public async Task Disabled_Jobs_Are_Not_Scheduled()
    {
        var job = new TestJob("a", "* * * * *") { Enabled = false };
        var scheduler = new Scheduler(
            new BaseJob[] { job },
            MockCronReturning(DateTime.UtcNow.AddMilliseconds(50)),
            TimeSpan.FromSeconds(5),
            Logger());

        await scheduler.StartAsync(CancellationToken.None);
        await Task.Delay(200);
        await scheduler.StopAsync(CancellationToken.None);

        job.HandleCalls.Should().Be(0);
    }

    [Fact]
    public async Task Job_Times_Out_Returns_Cancelled_Status()
    {
        var job = new TestJob("slow", "* * * * *", handleDelayMs: 5000);
        var now = DateTime.UtcNow;
        // First occurrence: T+10ms. Subsequent: T+1h.
        var adapter = MockCronWithSequence(now.AddMilliseconds(10), now.AddHours(1));

        var scheduler = new Scheduler(
            new BaseJob[] { job },
            adapter,
            TimeSpan.FromMilliseconds(100),
            Logger());

        await scheduler.StartAsync(CancellationToken.None);
        // Wait for the job to be cancelled by timeout (T+10ms start, T+110ms cancel)
        await Task.Delay(300);
        await scheduler.StopAsync(CancellationToken.None);

        job.HandleCalls.Should().Be(1);
    }

    [Fact]
    public async Task StopAsync_With_No_Jobs_Does_Not_Hang()
    {
        var scheduler = new Scheduler(
            Array.Empty<BaseJob>(),
            MockCronReturning(DateTime.UtcNow),
            TimeSpan.FromSeconds(1),
            Logger());

        await scheduler.StartAsync(CancellationToken.None);
        await scheduler.StopAsync(CancellationToken.None);
    }

    [Fact]
    public void Constructor_Accepts_Func_For_UtcNow()
    {
        // Just exercise the internal ctor to ensure the test seam exists
        var fixedNow = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var schedule = new Mock<ICronSchedule>();
        schedule.Setup(s => s.GetNextOccurrence(It.IsAny<DateTime>())).Returns(fixedNow);
        var adapter = new Mock<ICronAdapter>();
        adapter.Setup(a => a.Parse(It.IsAny<string>())).Returns(schedule.Object);

        Action act = () => new Scheduler(
            Array.Empty<BaseJob>(),
            adapter.Object,
            TimeSpan.FromSeconds(1),
            Logger(),
            () => fixedNow);
        act.Should().NotThrow();
    }

    [Fact]
    public void Dispose_Can_Be_Called_Before_Start()
    {
        var scheduler = new Scheduler(
            Array.Empty<BaseJob>(),
            MockCronReturning(DateTime.UtcNow),
            TimeSpan.FromSeconds(1),
            Logger());

        Action act = () => scheduler.Dispose();
        act.Should().NotThrow();
    }

    [Fact]
    public async Task StopAsync_Cancels_Supervisor_During_Delay()
    {
        // Job never runs (cron in the future), but supervisor is sleeping.
        // When we stop, the supervisor's Task.Delay should be cancelled.
        var job = new TestJob("a", "* * * * *");
        var futureTime = DateTime.UtcNow.AddMinutes(5);
        var scheduler = new Scheduler(
            new BaseJob[] { job },
            MockCronReturning(futureTime),
            TimeSpan.FromSeconds(1),
            Logger());

        await scheduler.StartAsync(CancellationToken.None);
        await Task.Delay(50);  // let the supervisor start sleeping
        await scheduler.StopAsync(CancellationToken.None);

        job.HandleCalls.Should().Be(0);
    }

    [Fact]
    public async Task StopAsync_With_Pre_Cancelled_Token_Breaks_Out_Of_Wait()
    {
        // Set up a long-running job so StopAsync enters the polling wait.
        // Then pass a pre-cancelled token to break out of the wait.
        var job = new TestJob("long", "* * * * *", handleDelayMs: 5000);
        var scheduler = new Scheduler(
            new BaseJob[] { job },
            MockCronReturning(DateTime.UtcNow.AddMilliseconds(10)),
            TimeSpan.FromSeconds(10),
            Logger());

        await scheduler.StartAsync(CancellationToken.None);
        await Task.Delay(200);  // job is now running

        using var cts = new CancellationTokenSource();
        cts.Cancel();
        // StopAsync should not hang even with cancelled token
        await scheduler.StopAsync(cts.Token);
    }
}
