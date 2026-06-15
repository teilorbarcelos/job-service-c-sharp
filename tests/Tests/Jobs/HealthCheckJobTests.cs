using FluentAssertions;
using JobService.Core;
using JobService.Infrastructure.Health;
using JobService.Jobs;
using JobService.Shared.Config;
using Microsoft.Extensions.Options;
using Moq;
using Serilog;
using Xunit;

namespace JobService.Tests.Jobs;

public class HealthCheckJobTests
{
    [Fact]
    public void Job_Has_Expected_Metadata()
    {
        var checker = new Mock<IHealthChecker>().Object;
        var job = new HealthCheckJob(checker, Options.Create(new AppSettings { HealthCheckCron = "0 9 * * *" }));
        job.Name.Should().Be("health-check");
        job.Schedule.Should().Be("0 9 * * *");
        job.Description.Should().Contain("SQL Server");
        job.Description.Should().Contain("Redis");
        job.Description.Should().Contain("RabbitMQ");
        job.Enabled.Should().BeTrue();
    }

    [Fact]
    public void Job_Default_Schedule_Comes_From_Settings()
    {
        var checker = new Mock<IHealthChecker>().Object;
        var job = new HealthCheckJob(checker, Options.Create(new AppSettings()));
        job.Schedule.Should().Be("*/1 * * * *");
    }

    [Fact]
    public void Job_Enabled_Can_Be_Toggled()
    {
        var checker = new Mock<IHealthChecker>().Object;
        var job = new HealthCheckJob(checker, Options.Create(new AppSettings()));
        job.Enabled = false;
        job.Enabled.Should().BeFalse();
    }

    [Fact]
    public void Ctor_Throws_On_Null_Checker()
    {
        Action act = () => new HealthCheckJob(null!, Options.Create(new AppSettings()));
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public async Task Handle_Logs_And_Prints_When_All_Up()
    {
        var checker = new Mock<IHealthChecker>();
        checker.Setup(c => c.CheckSqlAsync()).ReturnsAsync(new HealthCheckResult(HealthStatus.Up, 5));
        checker.Setup(c => c.CheckRedisAsync()).ReturnsAsync(new HealthCheckResult(HealthStatus.Up, 1));
        checker.Setup(c => c.CheckRabbitAsync()).ReturnsAsync(new HealthCheckResult(HealthStatus.Up));

        var job = new HealthCheckJob(checker.Object, Options.Create(new AppSettings()));
        var ctx = new JobContext { Logger = new LoggerConfiguration().CreateLogger() };

        using var sw = new StringWriter();
        var originalOut = Console.Out;
        Console.SetOut(sw);
        try
        {
            await job.HandleAsync(ctx, CancellationToken.None);
        }
        finally
        {
            Console.SetOut(originalOut);
        }

        sw.ToString().Should().Contain("sql=Up");
        sw.ToString().Should().Contain("redis=Up");
        sw.ToString().Should().Contain("rabbitmq=Up");
    }

    [Fact]
    public async Task Handle_Reports_Degraded_When_Sql_Down()
    {
        var checker = new Mock<IHealthChecker>();
        checker.Setup(c => c.CheckSqlAsync()).ReturnsAsync(new HealthCheckResult(HealthStatus.Down, Error: "conn refused"));
        checker.Setup(c => c.CheckRedisAsync()).ReturnsAsync(new HealthCheckResult(HealthStatus.Up));
        checker.Setup(c => c.CheckRabbitAsync()).ReturnsAsync(new HealthCheckResult(HealthStatus.Up));

        var job = new HealthCheckJob(checker.Object, Options.Create(new AppSettings()));
        var ctx = new JobContext { Logger = new LoggerConfiguration().CreateLogger() };

        using var sw = new StringWriter();
        var originalOut = Console.Out;
        Console.SetOut(sw);
        try
        {
            await job.HandleAsync(ctx, CancellationToken.None);
        }
        finally
        {
            Console.SetOut(originalOut);
        }

        sw.ToString().Should().Contain("sql=Down");
    }

    [Fact]
    public async Task Handle_Reports_Degraded_When_Redis_Disabled()
    {
        var checker = new Mock<IHealthChecker>();
        checker.Setup(c => c.CheckSqlAsync()).ReturnsAsync(new HealthCheckResult(HealthStatus.Up));
        checker.Setup(c => c.CheckRedisAsync()).ReturnsAsync(new HealthCheckResult(HealthStatus.Disabled));
        checker.Setup(c => c.CheckRabbitAsync()).ReturnsAsync(new HealthCheckResult(HealthStatus.Up));

        var job = new HealthCheckJob(checker.Object, Options.Create(new AppSettings()));
        var ctx = new JobContext { Logger = new LoggerConfiguration().CreateLogger() };

        using var sw = new StringWriter();
        var originalOut = Console.Out;
        Console.SetOut(sw);
        try
        {
            await job.HandleAsync(ctx, CancellationToken.None);
        }
        finally
        {
            Console.SetOut(originalOut);
        }

        sw.ToString().Should().Contain("redis=Disabled");
    }
}
