using System.Globalization;
using System.Text;
namespace ScaleSim.Core;

public static class FrameEncoder
{
    public static byte[] Encode(int weight)
    {
        if (weight is < 0 or > 99999) throw new ArgumentOutOfRangeException(nameof(weight));
        return Encoding.ASCII.GetBytes("\x02   " + weight.ToString("D5", CultureInfo.InvariantCulture) + "LG \r\n");
    }
    public static int Parse(string text)
    {
        if (text.Length == 0 || text.Any(c => c is < '0' or > '9') ||
            !int.TryParse(text, out var weight) || weight > 99999)
            throw new ArgumentException("Enter an integer weight from 0 to 99999.");
        return weight;
    }
}
public sealed record AutoSettings(int Start = 20000, int Minimum = 0, int Decrement = 20, int Repeats = 10, int IntervalMs = 500)
{
    public void Validate()
    {
        FrameEncoder.Encode(Start); FrameEncoder.Encode(Minimum);
        if (Minimum > Start) throw new ArgumentException("Minimum must not exceed starting weight.");
        if (Decrement is < 1 or > 99999) throw new ArgumentException("Decrement must be 1–99999.");
        if (Repeats is < 10 or > 1000) throw new ArgumentException("Repeat count must be 10–1000.");
        if (IntervalMs is < 10 or > 60000) throw new ArgumentException("Interval must be 10–60000 ms.");
    }
}
public sealed record FrameTicket(int Weight, byte[] Bytes, long Version);
public sealed record ScaleState(int Weight, int Repeated, int Required, long Completed, bool Automatic, bool Paused);
public sealed class WeightEngine
{
    readonly object gate = new();
    AutoSettings settings = new();
    int weight = 20000, repeated;
    long completed, version;
    bool automatic, paused;
    public AutoSettings Settings { get { lock(gate) return settings; } }
    public ScaleState State { get { lock(gate) return new(weight, repeated, settings.Repeats, completed, automatic, paused); } }
    public void Configure(AutoSettings value) { value.Validate(); lock(gate) { settings = value; ResetLocked(); } }
    public void StartAutomatic(AutoSettings value)
    {
        value.Validate();
        lock(gate) { settings = value; automatic = true; ResetLocked(); }
    }
    public void SetInterval(int milliseconds)
    {
        lock(gate) { var updated = settings with { IntervalMs = milliseconds }; updated.Validate(); settings = updated; }
    }
    public void Manual(int value) { FrameEncoder.Encode(value); lock(gate) { automatic = false; weight = value; repeated = 0; version++; } }
    public void Automatic() { lock(gate) { automatic = true; ResetLocked(); } }
    public void Reset() { lock(gate) ResetLocked(); }
    void ResetLocked() { if(automatic) weight = settings.Start; repeated = 0; completed = 0; version++; }
    public void Pause(bool value) { lock(gate) paused = value; }
    public void Reconnected() { lock(gate) { repeated = 0; version++; } }
    public FrameTicket? Next()
    {
        lock(gate)
        {
            if(paused) return null;
            // Preparing a frame must not advance a cycle. A failed boundary write leaves
            // the retained weight/count intact, so a reconnect can give that weight a full hold.
            int candidate = automatic && repeated >= settings.Repeats
                ? weight - settings.Decrement < settings.Minimum ? settings.Start : weight - settings.Decrement
                : weight;
            return new(candidate, FrameEncoder.Encode(candidate), ++version);
        }
    }
    // Commit only after a complete transport write; stale writes cannot advance a changed session.
    public void Commit(FrameTicket ticket)
    {
        lock(gate)
        {
            if(ticket.Version != version) return;
            version++; // A ticket may account for at most one completed write.
            if(automatic && repeated >= settings.Repeats) { weight = ticket.Weight; repeated = 0; }
            completed++;
            if(repeated < int.MaxValue) repeated++;
        }
    }
}
