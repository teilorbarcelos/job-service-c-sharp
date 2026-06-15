using FluentAssertions;
using JobService.Core;
using Serilog;
using Serilog.Core;
using Xunit;

namespace JobService.Tests.Core;

public class BaseJobTests
{
    private sealed class SlowJob : BaseJob
    {
        public override string Name => "slow";
        public override string Schedule => "* * * * *";
        public int HandleCalls { get; private set; }
        public override async Task HandleAsync(JobContext context, CancellationToken cancellationToken)
        {
            HandleCalls++;
            await Task.Delay(200, cancellationToken);
        }
    }

    private sealed class ThrowingJob : BaseJob
    {
        public override string Name => "boom";
        public override string Schedule => "* * * * *";
        public override Task HandleAsync(JobContext context, CancellationToken cancellationToken)
            => throw new InvalidOperationException("kaboom");
    }

    private sealed class CancellableJob : BaseJob
    {
        public override string Name => "cancellable";
        public override string Schedule => "* * * * *";
        public override async Task HandleAsync(JobContext context, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await Task.Delay(5000, cancellationToken);
        }
    }

    private sealed class CustomJob : BaseJob
    {
        public override string Name => "custom";
        public override string Schedule => "0 0 * * *";
        public override string Description => "custom desc";
        public override Task HandleAsync(JobContext context, CancellationToken cancellationToken)
            => Task.CompletedTask;
    }

    private static ILogger Logger() => new LoggerConfiguration().CreateLogger();

    [Fact]
    public async Task RunAsync_Returns_Success_On_Handle_Completion()
    {
        var job = new SlowJob();
        var result = await job.RunAsync(Logger(), CancellationToken.None);

        result.Job.Should().Be("slow");
        result.Status.Should().Be(JobStatus.Success);
        result.DurationMs.Should().BeGreaterThanOrEqualTo(0);
        result.Error.Should().BeNull();
        job.HandleCalls.Should().Be(1);
    }

    [Fact]
    public async Task RunAsync_Returns_Failed_When_Handle_Throws()
    {
        var job = new ThrowingJob();
        var result = await job.RunAsync(Logger(), CancellationToken.None);

        result.Status.Should().Be(JobStatus.Failed);
        result.Error.Should().Be("kaboom");
    }

    [Fact]
    public async Task RunAsync_Returns_Cancelled_When_Cancellation_Requested()
    {
        var job = new CancellableJob();
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        var result = await job.RunAsync(Logger(), cts.Token);

        result.Status.Should().Be(JobStatus.Cancelled);
    }

    [Fact]
    public async Task RunAsync_Propagates_Cancellation_To_Handle()
    {
        var job = new CancellableJob();
        using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(50));
        var result = await job.RunAsync(Logger(), cts.Token);

        result.Status.Should().Be(JobStatus.Cancelled);
    }

    [Fact]
    public void Default_Description_Is_Empty_And_Enabled_Is_True()
    {
        var job = new CustomJob();
        job.Description.Should().Be("custom desc");
        job.Enabled.Should().BeTrue();
    }

    [Fact]
    public void Default_Description_Is_Empty_When_Not_Overridden()
    {
        var job = new DefaultDescriptionJob();
        job.Description.Should().BeEmpty();
    }

    private sealed class DefaultDescriptionJob : BaseJob
    {
        public override string Name => "no-desc";
        public override string Schedule => "* * * * *";
        public override Task HandleAsync(JobContext context, CancellationToken cancellationToken)
            => Task.CompletedTask;
    }

    [Fact]
    public void Enabled_Can_Be_Toggled()
    {
        var job = new CustomJob();
        job.Enabled = false;
        job.Enabled.Should().BeFalse();
    }
}
