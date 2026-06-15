using FluentAssertions;
using JobService.Infrastructure.Redis;
using JobService.Shared.Config;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using StackExchange.Redis;
using Xunit;

namespace JobService.Tests.Infrastructure.Redis;

public class RedisProviderTests
{
    [Fact]
    public void Ctor_Throws_On_Null_Options()
    {
        Action act = () => new RedisProvider(null!, NullLogger<RedisProvider>.Instance);
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Ctor_Throws_On_Null_Logger()
    {
        var settings = Options.Create(new AppSettings());
        Action act = () => new RedisProvider(settings, null!);
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Ctor_With_Redis_Url_Parses_Successfully()
    {
        var settings = Options.Create(new AppSettings
        {
            RedisHost = "redis://localhost:6379",
        });
        var provider = new RedisProvider(settings, NullLogger<RedisProvider>.Instance);
        provider.Should().NotBeNull();
    }

    [Fact]
    public void Ctor_With_Rediss_Url_Parses_Successfully()
    {
        var settings = Options.Create(new AppSettings
        {
            RedisHost = "rediss://secure.example.com:6380",
        });
        var provider = new RedisProvider(settings, NullLogger<RedisProvider>.Instance);
        provider.Should().NotBeNull();
    }

    [Fact]
    public void Ctor_With_Host_And_Port_Builds_Configuration()
    {
        var settings = Options.Create(new AppSettings
        {
            RedisHost = "redis.example.com",
            RedisPort = 6380,
            RedisPassword = "secret",
            RedisDb = 2,
        });
        var provider = new RedisProvider(settings, NullLogger<RedisProvider>.Instance);
        provider.Should().NotBeNull();
    }

    [Fact]
    public void Dispose_Is_Idempotent()
    {
        var provider = new RedisProvider(
            Options.Create(new AppSettings { RedisHost = "localhost" }),
            NullLogger<RedisProvider>.Instance);
        provider.Dispose();
        provider.Dispose();
    }

    [Fact]
    public async Task PingAsync_Returns_False_When_Not_Connected()
    {
        var provider = new RedisProvider(
            Options.Create(new AppSettings
            {
                RedisHost = "redis://nonexistent.invalid:9999",
                RedisCommandTimeoutSeconds = 1,
            }),
            NullLogger<RedisProvider>.Instance);

        var result = await provider.PingAsync();
        result.Should().BeFalse();
    }

    [Fact]
    public async Task PingAsync_With_Host_Port_Path_Builds_Config_And_Fails_Gracefully()
    {
        var provider = new RedisProvider(
            Options.Create(new AppSettings
            {
                RedisHost = "nonexistent.invalid",
                RedisPort = 9999,
                RedisPassword = "secret",
                RedisDb = 2,
                RedisCommandTimeoutSeconds = 1,
            }),
            NullLogger<RedisProvider>.Instance);

        var result = await provider.PingAsync();
        result.Should().BeFalse();
    }

    [Fact]
    public void Dispose_With_No_Connection_Skips_Inner_Dispose()
    {
        var provider = new RedisProvider(
            Options.Create(new AppSettings { RedisHost = "localhost" }),
            NullLogger<RedisProvider>.Instance);
        provider.Dispose();
    }

    [Fact]
    public async Task Dispose_After_Failed_Connection_Disposes_Inner()
    {
        var provider = new RedisProvider(
            Options.Create(new AppSettings
            {
                RedisHost = "nonexistent.invalid",
                RedisPort = 9999,
                RedisCommandTimeoutSeconds = 1,
            }),
            NullLogger<RedisProvider>.Instance);

        await provider.PingAsync();
        provider.Dispose();
    }

    [Fact]
    public async Task PingAsync_Returns_True_When_Pong_Positive()
    {
        var db = new Mock<IDatabase>();
        db.Setup(d => d.PingAsync(It.IsAny<CommandFlags>())).ReturnsAsync(TimeSpan.FromMilliseconds(5));

        var provider = new TestableRedisProvider(
            Options.Create(new AppSettings { RedisHost = "localhost" }),
            NullLogger<RedisProvider>.Instance,
            db.Object);

        var result = await provider.PingAsync();
        result.Should().BeTrue();
    }

    [Fact]
    public async Task PingAsync_Returns_False_When_Pong_Zero()
    {
        var db = new Mock<IDatabase>();
        db.Setup(d => d.PingAsync(It.IsAny<CommandFlags>())).ReturnsAsync(TimeSpan.Zero);

        var provider = new TestableRedisProvider(
            Options.Create(new AppSettings { RedisHost = "localhost" }),
            NullLogger<RedisProvider>.Instance,
            db.Object);

        var result = await provider.PingAsync();
        result.Should().BeFalse();
    }
}

internal class TestableRedisProvider : RedisProvider
{
    private readonly IDatabase _db;

    public TestableRedisProvider(
        IOptions<AppSettings> options,
        ILogger<RedisProvider> logger,
        IDatabase db) : base(options, logger)
    {
        _db = db;
    }

    public override IDatabase GetDatabase() => _db;
}
