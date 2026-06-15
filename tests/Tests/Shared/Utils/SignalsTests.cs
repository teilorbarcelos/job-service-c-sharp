using FluentAssertions;
using JobService.Shared.Utils;
using Xunit;

namespace JobService.Tests.Shared.Utils;

public class TimeoutCtsTests
{
    [Fact]
    public void Create_Returns_Linked_Source_That_Respects_Timeout()
    {
        using var parent = new CancellationTokenSource();
        using var cts = TimeoutCts.Create(TimeSpan.FromMilliseconds(50), parent.Token);

        cts.IsCancellationRequested.Should().BeFalse();
        Thread.Sleep(200);
        cts.IsCancellationRequested.Should().BeTrue();
    }

    [Fact]
    public void Create_With_Zero_Timeout_Does_Not_Auto_Cancel()
    {
        using var parent = new CancellationTokenSource();
        using var cts = TimeoutCts.Create(TimeSpan.Zero, parent.Token);

        cts.IsCancellationRequested.Should().BeFalse();
        Thread.Sleep(100);
        cts.IsCancellationRequested.Should().BeFalse();
    }

    [Fact]
    public void Create_Propagates_Parent_Cancellation()
    {
        using var parent = new CancellationTokenSource();
        using var cts = TimeoutCts.Create(TimeSpan.FromSeconds(10), parent.Token);

        parent.Cancel();
        cts.IsCancellationRequested.Should().BeTrue();
    }
}
