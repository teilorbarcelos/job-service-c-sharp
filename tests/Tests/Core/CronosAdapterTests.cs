using FluentAssertions;
using JobService.Core;
using Serilog.Core;
using Xunit;

namespace JobService.Tests.Core;

public class CronosAdapterTests
{
    [Fact]
    public void Parse_Returns_Schedule_With_Next_Occurrence()
    {
        var adapter = new CronosAdapter();
        var schedule = adapter.Parse("*/5 * * * *");
        var from = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var next = schedule.GetNextOccurrence(from);
        next.Minute.Should().Be(5);
    }

    [Fact]
    public void Parse_Throws_On_Invalid_Expression()
    {
        var adapter = new CronosAdapter();
        Action act = () => adapter.Parse("not a cron");
        act.Should().Throw<Cronos.CronFormatException>();
    }

    [Fact]
    public void Parse_Throws_On_Null()
    {
        var adapter = new CronosAdapter();
        Action act = () => adapter.Parse(null!);
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void GetNextOccurrence_Returns_Next_Year_When_Date_Passed()
    {
        var adapter = new CronosAdapter();
        var schedule = adapter.Parse("0 0 1 1 *");
        var from = new DateTime(2026, 6, 15, 12, 0, 0, DateTimeKind.Utc);
        var next = schedule.GetNextOccurrence(from);
        next.Should().Be(new DateTime(2027, 1, 1, 0, 0, 0, DateTimeKind.Utc));
    }
}
