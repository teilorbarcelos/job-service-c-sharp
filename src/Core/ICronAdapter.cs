using Cronos;

namespace JobService.Core;

public interface ICronAdapter
{
    ICronSchedule Parse(string expression);
}

public interface ICronSchedule
{
    DateTime GetNextOccurrence(DateTime fromUtc);
}

public sealed class CronosAdapter : ICronAdapter
{
    public ICronSchedule Parse(string expression)
    {
        ArgumentNullException.ThrowIfNull(expression);
        var cron = CronExpression.Parse(expression);
        return new CronosSchedule(cron);
    }
}

internal sealed class CronosSchedule : ICronSchedule
{
    private readonly CronExpression _cron;

    public CronosSchedule(CronExpression cron)
    {
        _cron = cron;
    }

    public DateTime GetNextOccurrence(DateTime fromUtc)
    {
        var next = _cron.GetNextOccurrence(fromUtc);
        return next ?? fromUtc;
    }
}
