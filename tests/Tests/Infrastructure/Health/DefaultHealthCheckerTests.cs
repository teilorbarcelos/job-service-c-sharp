using FluentAssertions;
using JobService.Infrastructure.Database;
using JobService.Infrastructure.Health;
using JobService.Infrastructure.Messaging;
using JobService.Infrastructure.Redis;
using JobService.Shared.Config;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace JobService.Tests.Infrastructure.Health;

public class DefaultHealthCheckerTests
{
    private static Mock<ISqlProvider> MockSql(bool pingResult = true, Exception? throws = null)
    {
        var mock = new Mock<ISqlProvider>();
        if (throws != null)
            mock.Setup(s => s.PingAsync(It.IsAny<CancellationToken>())).ThrowsAsync(throws);
        else
            mock.Setup(s => s.PingAsync(It.IsAny<CancellationToken>())).ReturnsAsync(pingResult);
        return mock;
    }

    private static Mock<IRedisProvider> MockRedis(bool pingResult = true, Exception? throws = null)
    {
        var mock = new Mock<IRedisProvider>();
        if (throws != null)
            mock.Setup(r => r.PingAsync()).ThrowsAsync(throws);
        else
            mock.Setup(r => r.PingAsync()).ReturnsAsync(pingResult);
        return mock;
    }

    private static Mock<IRabbitMqProvider> MockRabbit(bool check = true, Exception? throws = null)
    {
        var mock = new Mock<IRabbitMqProvider>();
        if (throws != null)
            mock.Setup(r => r.Check()).Throws(throws);
        else
            mock.Setup(r => r.Check()).Returns(check);
        return mock;
    }

    [Fact]
    public async Task CheckSql_Returns_Up_When_Ping_True()
    {
        var checker = new DefaultHealthChecker(
            MockSql(true).Object,
            MockRedis().Object,
            MockRabbit().Object,
            Options.Create(new AppSettings()));

        var result = await checker.CheckSqlAsync();
        result.Status.Should().Be(HealthStatus.Up);
    }

    [Fact]
    public async Task CheckSql_Returns_Down_When_Ping_False()
    {
        var checker = new DefaultHealthChecker(
            MockSql(false).Object,
            MockRedis().Object,
            MockRabbit().Object,
            Options.Create(new AppSettings()));

        var result = await checker.CheckSqlAsync();
        result.Status.Should().Be(HealthStatus.Down);
        result.Error.Should().Be("ping returned false");
    }

    [Fact]
    public async Task CheckSql_Returns_Down_On_Exception()
    {
        var checker = new DefaultHealthChecker(
            MockSql(throws: new Exception("boom")).Object,
            MockRedis().Object,
            MockRabbit().Object,
            Options.Create(new AppSettings()));

        var result = await checker.CheckSqlAsync();
        result.Status.Should().Be(HealthStatus.Down);
        result.Error.Should().Be("boom");
    }

    [Fact]
    public async Task CheckRedis_Returns_Up_When_Ping_True()
    {
        var checker = new DefaultHealthChecker(
            MockSql().Object,
            MockRedis(true).Object,
            MockRabbit().Object,
            Options.Create(new AppSettings()));

        var result = await checker.CheckRedisAsync();
        result.Status.Should().Be(HealthStatus.Up);
    }

    [Fact]
    public async Task CheckRedis_Returns_Down_When_Ping_False()
    {
        var checker = new DefaultHealthChecker(
            MockSql().Object,
            MockRedis(false).Object,
            MockRabbit().Object,
            Options.Create(new AppSettings()));

        var result = await checker.CheckRedisAsync();
        result.Status.Should().Be(HealthStatus.Down);
        result.Error.Should().Be("ping returned false");
    }

    [Fact]
    public async Task CheckRedis_Returns_Down_On_Exception()
    {
        var checker = new DefaultHealthChecker(
            MockSql().Object,
            MockRedis(throws: new Exception("redis boom")).Object,
            MockRabbit().Object,
            Options.Create(new AppSettings()));

        var result = await checker.CheckRedisAsync();
        result.Status.Should().Be(HealthStatus.Down);
        result.Error.Should().Be("redis boom");
    }

    [Fact]
    public async Task CheckRabbit_Returns_Disabled_When_Messaging_Disabled()
    {
        var checker = new DefaultHealthChecker(
            MockSql().Object,
            MockRedis().Object,
            MockRabbit().Object,
            Options.Create(new AppSettings { MessagingEnabled = false }));

        var result = await checker.CheckRabbitAsync();
        result.Status.Should().Be(HealthStatus.Disabled);
    }

    [Fact]
    public async Task CheckRabbit_Returns_Up_When_Check_True()
    {
        var checker = new DefaultHealthChecker(
            MockSql().Object,
            MockRedis().Object,
            MockRabbit(true).Object,
            Options.Create(new AppSettings { MessagingEnabled = true }));

        var result = await checker.CheckRabbitAsync();
        result.Status.Should().Be(HealthStatus.Up);
    }

    [Fact]
    public async Task CheckRabbit_Returns_Down_When_Check_False()
    {
        var checker = new DefaultHealthChecker(
            MockSql().Object,
            MockRedis().Object,
            MockRabbit(false).Object,
            Options.Create(new AppSettings { MessagingEnabled = true }));

        var result = await checker.CheckRabbitAsync();
        result.Status.Should().Be(HealthStatus.Down);
        result.Error.Should().Be("connection closed");
    }

    [Fact]
    public async Task CheckRabbit_Returns_Down_On_Exception()
    {
        var checker = new DefaultHealthChecker(
            MockSql().Object,
            MockRedis().Object,
            MockRabbit(throws: new Exception("rabbit boom")).Object,
            Options.Create(new AppSettings { MessagingEnabled = true }));

        var result = await checker.CheckRabbitAsync();
        result.Status.Should().Be(HealthStatus.Down);
        result.Error.Should().Be("rabbit boom");
    }
}
