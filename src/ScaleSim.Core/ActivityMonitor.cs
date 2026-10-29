namespace ScaleSim.Core;

public sealed record ActivityEntry(DateTimeOffset At, string Message, FrameTicket? Frame = null);
public sealed record ActivitySnapshot(long Writes, long Bytes, long Errors, DateTimeOffset? LastWrite, FrameTicket? LastFrame, TimeSpan? LastWriteAge = null)
{
    // A local write is evidence of feed activity, not proof that WasteWORKS captured it.
    // Allow ordinary scheduling jitter; known pause/disconnect/error states must also gate animation.
    public bool HasRecentWrite(int intervalMs)
    {
        if(intervalMs is < 10 or > 60000) throw new ArgumentOutOfRangeException(nameof(intervalMs));
        return Writes > 0 && LastWriteAge is {} age
            && age <= TimeSpan.FromMilliseconds(intervalMs + Math.Max(250, intervalMs / 2d));
    }
}

// The worker never queues UI callbacks. Both pending events and visible history are bounded.
public sealed class ActivityMonitor
{
    readonly object gate = new();
    readonly Queue<ActivityEntry> pending = new();
    readonly int capacity;
    readonly TimeProvider time;
    long writes, bytes, errors;
    DateTimeOffset? lastWrite;
    long? lastWriteTimestamp;
    FrameTicket? lastFrame;
    public ActivityMonitor(int capacity = 500, TimeProvider? timeProvider = null)
    {
        if(capacity < 1) throw new ArgumentOutOfRangeException(nameof(capacity));
        this.capacity = capacity;
        time = timeProvider ?? TimeProvider.System;
    }
    void Add(ActivityEntry entry) { while(pending.Count >= capacity) pending.Dequeue(); pending.Enqueue(entry); }
    public void Sent(FrameTicket frame)
    {
        lock(gate) {
            writes++; bytes += frame.Bytes.Length;
            lastWrite = time.GetLocalNow(); lastWriteTimestamp = time.GetTimestamp(); lastFrame = frame;
            Add(new(lastWrite.Value, "Frame sent", frame));
        }
    }
    public void Notice(string message, bool error = false)
    {
        lock(gate) { if(error) errors++; Add(new(time.GetLocalNow(), message)); }
    }
    public ActivitySnapshot Snapshot
    {
        get {
            lock(gate) return new(writes, bytes, errors, lastWrite, lastFrame,
                lastWriteTimestamp is {} timestamp ? time.GetElapsedTime(timestamp) : null);
        }
    }
    public ActivityEntry[] Drain() { lock(gate) { var result = pending.ToArray(); pending.Clear(); return result; } }
    public void Reset()
    {
        lock(gate) { writes = bytes = errors = 0; lastWrite = null; lastWriteTimestamp = null; lastFrame = null; pending.Clear(); }
    }
}
