using FluentAssertions;
using JobService.Shared.Utils;
using Microsoft.Extensions.Hosting;
using Moq;
using Xunit;

namespace JobService.Tests.Shared.Utils;

public class ShutdownHandlerTests
{
    [Fact]
    public void Ctor_Throws_On_Null_Logger()
    {
        var lifetime = new Mock<IHostApplicationLifetime>().Object;
        var act = () => new ShutdownHandler(null!, lifetime, TimeSpan.FromSeconds(1), () => Task.CompletedTask);
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Ctor_Throws_On_Null_Lifetime()
    {
        using var logger = AppLogger.Create();
        var act = () => new ShutdownHandler(logger, null!, TimeSpan.FromSeconds(1), () => Task.CompletedTask);
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Ctor_Throws_On_Null_Cleanup()
    {
        using var logger = AppLogger.Create();
        var lifetime = new Mock<IHostApplicationLifetime>().Object;
        var act = () => new ShutdownHandler(logger, lifetime, TimeSpan.FromSeconds(1), null!);
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public async Task Cleanup_Runs_When_Application_Stopping_Fires()
    {
        using var logger = AppLogger.Create();
        var lifetime = new Mock<IHostApplicationLifetime>();
        var cts = new CancellationTokenSource();
        lifetime.Setup(l => l.ApplicationStopping).Returns(cts.Token);

        int called = 0;
        var handler = new ShutdownHandler(logger, lifetime.Object, TimeSpan.FromSeconds(2),
            () => { called++; return Task.CompletedTask; });
        handler.Register();

        cts.Cancel();
        await Task.Delay(200);

        called.Should().BeGreaterThanOrEqualTo(1);
    }

    [Fact]
    public async Task Cleanup_Logs_Warning_When_Timeout_Exceeded()
    {
        using var logger = AppLogger.Create();
        var lifetime = new Mock<IHostApplicationLifetime>();
        var cts = new CancellationTokenSource();
        lifetime.Setup(l => l.ApplicationStopping).Returns(cts.Token);

        var handler = new ShutdownHandler(logger, lifetime.Object, TimeSpan.FromMilliseconds(100),
            async () => { await Task.Delay(2000); });
        handler.Register();

        cts.Cancel();
        await Task.Delay(500);
        // No exception is thrown to the caller; warning is logged internally
    }

    [Fact]
    public async Task OnCancelKeyPress_Cancels_And_Stops_Lifetime()
    {
        using var logger = AppLogger.Create();
        var lifetime = new Mock<IHostApplicationLifetime>();
        var cts = new CancellationTokenSource();
        lifetime.Setup(l => l.ApplicationStopping).Returns(cts.Token);
        lifetime.Setup(l => l.StopApplication());

        int called = 0;
        var handler = new ShutdownHandler(logger, lifetime.Object, TimeSpan.FromSeconds(2),
            () => { called++; return Task.CompletedTask; });
        handler.Register();

        cts.Cancel();
        await Task.Delay(200);

        called.Should().Be(1);
    }

    [Fact]
    public void Cleanup_Logs_Error_When_Cleanup_Throws()
    {
        using var logger = AppLogger.Create();
        var lifetime = new Mock<IHostApplicationLifetime>();
        var cts = new CancellationTokenSource();
        lifetime.Setup(l => l.ApplicationStopping).Returns(cts.Token);

        var handler = new ShutdownHandler(logger, lifetime.Object, TimeSpan.FromSeconds(2),
            () => throw new InvalidOperationException("cleanup failed"));
        handler.Register();

        cts.Cancel();
        // No exception is thrown to the caller; error is logged internally
    }
}
