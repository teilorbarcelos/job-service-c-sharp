namespace JobService.Shared.Utils;

public static class TimeoutCts
{
    public static CancellationTokenSource Create(TimeSpan timeout, CancellationToken parent)
    {
        var cts = CancellationTokenSource.CreateLinkedTokenSource(parent);
        if (timeout > TimeSpan.Zero)
            cts.CancelAfter(timeout);
        return cts;
    }
}
