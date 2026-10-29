namespace ScaleSim.Core;

public sealed record ActivityEntry(DateTimeOffset At, string Message, FrameTicket? Frame = null);
public sealed record ActivitySnapshot(long Writes, long Bytes, long Errors, DateTimeOffset? LastWrite, FrameTicket? LastFrame);

// The worker never queues UI callbacks. Both pending events and visible history are bounded.
public sealed class ActivityMonitor
{
    readonly object gate = new();
    readonly Queue<ActivityEntry> pending = new();
    readonly int capacity;
    long writes, errors;
    DateTimeOffset? lastWrite;
    FrameTicket? lastFrame;
    public ActivityMonitor(int capacity = 500)
    {
        if(capacity < 1) throw new ArgumentOutOfRangeException(nameof(capacity));
        this.capacity = capacity;
    }
    void Add(ActivityEntry entry) { while(pending.Count >= capacity) pending.Dequeue(); pending.Enqueue(entry); }
    public void Sent(FrameTicket frame)
    {
        lock(gate) {
            writes++; lastWrite = DateTimeOffset.Now; lastFrame = frame;
            Add(new(lastWrite.Value, "Frame sent", frame));
        }
    }
    public void Notice(string message, bool error = false)
    {
        lock(gate) { if(error) errors++; Add(new(DateTimeOffset.Now, message)); }
    }
    public ActivitySnapshot Snapshot { get { lock(gate) return new(writes, writes * 14, errors, lastWrite, lastFrame); } }
    public ActivityEntry[] Drain() { lock(gate) { var result = pending.ToArray(); pending.Clear(); return result; } }
    public void Reset() { lock(gate) { writes = errors = 0; lastWrite = null; lastFrame = null; pending.Clear(); } }
}
