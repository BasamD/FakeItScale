using System.Diagnostics;
namespace ScaleSim.Core;
public static class FrameClock
{
    // Re-read timing changes without waking the UI or blocking a disconnect for a long interval.
    public static async Task Wait(WeightEngine engine, CancellationToken cancellationToken)
    {
        var elapsed = Stopwatch.StartNew();
        while(true)
        {
            long remaining = engine.Settings.IntervalMs - elapsed.ElapsedMilliseconds;
            if(remaining <= 0) return;
            await Task.Delay((int)Math.Min(remaining, 50), cancellationToken);
        }
    }
}
