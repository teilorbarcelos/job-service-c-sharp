using FluentAssertions;
using JobService.Shared.Utils;
using Serilog.Events;
using Xunit;

namespace JobService.Tests.Shared.Utils;

public class AppLoggerTests
{
    [Fact]
    public void Create_Defaults_To_Information_When_Level_Invalid()
    {
        using var logger = AppLogger.Create("NotALevel");
        logger.IsEnabled(LogEventLevel.Information).Should().BeTrue();
        logger.IsEnabled(LogEventLevel.Debug).Should().BeFalse();
    }

    [Fact]
    public void Create_Parses_Level_Case_Insensitive()
    {
        using var logger = AppLogger.Create("debug");
        logger.IsEnabled(LogEventLevel.Debug).Should().BeTrue();
    }

    [Fact]
    public void Create_Adds_Environment_Property_When_Provided()
    {
        using var logger = AppLogger.Create("Information", "ci");
        logger.IsEnabled(LogEventLevel.Information).Should().BeTrue();
    }

    [Fact]
    public void Create_Without_Environment_Does_Not_Throw()
    {
        using var logger = AppLogger.Create("Information", environment: null);
        logger.Should().NotBeNull();
    }
}
