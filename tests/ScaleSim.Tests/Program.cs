using System.Net;
using System.Net.Sockets;
using System.Text;
using ScaleSim.Core;
int passed=0, failed=0;
void Equal<T>(T expected, T actual) { if(!EqualityComparer<T>.Default.Equals(expected,actual)) throw new Exception($"Expected {expected}; got {actual}"); }
void Reject(Action action) { try { action(); } catch(ArgumentException) { return; } throw new Exception("Invalid input accepted"); }
void Test(string name, Action action) { try { action(); Console.WriteLine("PASS " + name); passed++; } catch(Exception ex) { Console.WriteLine("FAIL " + name + ": " + ex); failed++; } }
async Task TestAsync(string name, Func<Task> action) { try { await action(); Console.WriteLine("PASS " + name); passed++; } catch(Exception ex) { Console.WriteLine("FAIL " + name + ": " + ex); failed++; } }
FrameTicket Send(WeightEngine e) { var t=e.Next() ?? throw new Exception("Unexpected pause"); e.Commit(t); return t; }
Test("Exact 14-byte protocol and zero padding", () => {
    Equal("0220202032303030304C47200D0A",Convert.ToHexString(FrameEncoder.Encode(20000)));
    Equal(14,FrameEncoder.Encode(99999).Length);
    Equal("\u0002   00000LG \r\n",Encoding.ASCII.GetString(FrameEncoder.Encode(0)));
});
Test("Invalid integer inputs and auto settings rejected", () => {
    foreach(var text in new[]{"-1","1.5","100000",""," 1","+1","1e3"}) Reject(()=>FrameEncoder.Parse(text));
    Reject(()=>FrameEncoder.Encode(-1)); Reject(()=>FrameEncoder.Encode(100000));
    foreach(var s in new[]{new AutoSettings(Minimum:30000),new AutoSettings(Decrement:0),new AutoSettings(Decrement:-1),new AutoSettings(Repeats:9),new AutoSettings(Repeats:1001),new AutoSettings(IntervalMs:0)}) Reject(s.Validate);
});
Test("First 30 auto transmissions hold for ten successful writes", () => {
    var e=new WeightEngine(); e.Automatic();
    for(int i=0;i<30;i++) Equal(20000-(i/10)*20,Send(e).Weight);
    Equal(30L,e.State.Completed);
});
Test("Manual weight stable for 150 sends and updates next frame", () => {
    var e=new WeightEngine(); e.Manual(20000);
    for(int i=0;i<150;i++) Equal(20000,Send(e).Weight);
    e.Manual(12000); Equal(12000,Send(e).Weight);
    e.Pause(true); Equal<FrameTicket?>(null,e.Next()); e.Pause(false); Equal(12000,Send(e).Weight);
});
Test("Pause at seven preserves remaining three sends", () => {
    var e=new WeightEngine(); e.Automatic(); for(int i=0;i<7;i++) Send(e);
    e.Pause(true); Equal<FrameTicket?>(null,e.Next()); Equal(7,e.State.Repeated);
    e.Pause(false); for(int i=0;i<3;i++) Equal(20000,Send(e).Weight);
    Equal(19980,Send(e).Weight);
});
Test("Minimum loop and non-divisible decrement", () => {
    var e=new WeightEngine(); e.Configure(new(100,60,20)); e.Automatic();
    for(int i=0;i<40;i++) Equal(new[]{100,80,60,100}[i/10],Send(e).Weight);
    e.Configure(new(100,65,20)); for(int i=0;i<30;i++) Equal(new[]{100,80,100}[i/10],Send(e).Weight);
});
Test("Failed writes do not count; reconnect restarts full hold", () => {
    var e=new WeightEngine(); e.Automatic(); for(int i=0;i<7;i++) Send(e);
    for(int i=0;i<20;i++) e.Next(); Equal(7,e.State.Repeated);
    e.Reconnected(); for(int i=0;i<10;i++) Equal(20000,Send(e).Weight);
    Equal(19980,Send(e).Weight);
});
Test("Reset, mode changes and stale write do not corrupt session", () => {
    var e=new WeightEngine(); e.Automatic(); var old=e.Next()!;
    e.Manual(12000); e.Commit(old); Equal(0L,e.State.Completed);
    e.Automatic(); Equal(20000,Send(e).Weight); e.Reset(); Equal(0L,e.State.Completed); Equal(0,e.State.Repeated);
    e.Configure(new(Repeats:12)); for(int i=0;i<12;i++) Equal(20000,Send(e).Weight); Equal(19980,Send(e).Weight);
});
async Task<byte[]> ReadFrame(NetworkStream stream) { var bytes=new byte[14]; using var timeout=new CancellationTokenSource(4000); await stream.ReadExactlyAsync(bytes,timeout.Token); return bytes; }
int FreePort() { var l=new TcpListener(IPAddress.Loopback,0); l.Start(); int p=((IPEndPoint)l.LocalEndpoint).Port; l.Stop(); return p; }
await TestAsync("Raw TCP frames, disconnect freeze, reconnect, stop/restart", async () => {
    var e=new WeightEngine(); e.Configure(new(IntervalMs:20)); e.Automatic(); int port=FreePort();
    var server=new TcpScaleServer(e,port); server.Start();
    try {
        using(var client=new TcpClient()) {
            await client.ConnectAsync(IPAddress.Loopback,port);
            for(int i=0;i<23;i++) {
                var frame=await ReadFrame(client.GetStream());
                Equal(Convert.ToHexString(FrameEncoder.Encode(20000-(i/10)*20)),Convert.ToHexString(frame));
            }
        }
        await Task.Delay(150); var frozen=e.State;
        await Task.Delay(150); Equal(frozen.Completed,e.State.Completed); Equal(frozen.Weight,e.State.Weight);
        using(var client=new TcpClient()) {
            await client.ConnectAsync(IPAddress.Loopback,port);
            for(int i=0;i<10;i++) Equal(Convert.ToHexString(FrameEncoder.Encode(frozen.Weight)),Convert.ToHexString(await ReadFrame(client.GetStream())));
        }
    } finally { await server.DisposeAsync(); }
    e.Reset(); await using var restarted=new TcpScaleServer(e,port); restarted.Start();
    using var again=new TcpClient(); await again.ConnectAsync(IPAddress.Loopback,port);
    Equal(Convert.ToHexString(FrameEncoder.Encode(20000)),Convert.ToHexString(await ReadFrame(again.GetStream())));
});
await TestAsync("Occupied port rejected and cleanup safe", async () => {
    int port=FreePort(); await using var a=new TcpScaleServer(new(),port); a.Start();
    await using var b=new TcpScaleServer(new(),port);
    try { b.Start(); } catch(SocketException) { return; }
    throw new Exception("Occupied port unexpectedly opened");
});
Test("Timing update preserves automatic hold, mode, and pause", () => {
    var e=new WeightEngine(); e.StartAutomatic(new(100,60,20));
    for(int i=0;i<7;i++) Send(e);
    e.Pause(true); var before=e.State; e.SetInterval(10);
    Equal(before,e.State); Equal(10,e.Settings.IntervalMs);
    Reject(()=>e.SetInterval(0)); Equal(before,e.State);
    e.Pause(false); for(int i=0;i<3;i++) Equal(100,Send(e).Weight); Equal(80,Send(e).Weight);
});
Test("Invalid automatic draft leaves the running session untouched", () => {
    var e=new WeightEngine(); e.StartAutomatic(new(100,60,20)); Send(e); var before=e.State;
    Reject(()=>e.StartAutomatic(new(100,101,20))); Equal(before,e.State);
});
Test("Pending activity stays bounded while accurate statistics retain every write", () => {
    var monitor=new ActivityMonitor(8); var e=new WeightEngine();
    for(int i=0;i<5000;i++) monitor.Sent(Send(e));
    monitor.Notice("write failed",true);
    Equal(5000L,monitor.Snapshot.Writes); Equal(70000L,monitor.Snapshot.Bytes); Equal(1L,monitor.Snapshot.Errors);
    Equal(8,monitor.Drain().Length); Equal(0,monitor.Drain().Length);
    monitor.Reset(); Equal(0L,monitor.Snapshot.Writes); Equal<FrameTicket?>(null,monitor.Snapshot.LastFrame);
});
await TestAsync("Timing changes shorten an existing long wait", async () => {
    var e=new WeightEngine(); e.SetInterval(60000);
    using var timeout=new CancellationTokenSource(2000);
    var wait=FrameClock.Wait(e,timeout.Token);
    await Task.Delay(60); e.SetInterval(10); await wait;
});
await TestAsync("Disconnect while paused with a 60-second interval accepts a new client promptly", async () => {
    var e=new WeightEngine(); e.StartAutomatic(new(IntervalMs:60000));
    int port=FreePort(); await using var server=new TcpScaleServer(e,port);
    int connections=0;
    var reconnected=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
    server.Status+=s=> { if(s.StartsWith("Connected:") && Interlocked.Increment(ref connections)==2) reconnected.TrySetResult(); };
    server.Start();
    using(var client=new TcpClient()) {
        await client.ConnectAsync(IPAddress.Loopback,port);
        await ReadFrame(client.GetStream()); e.Pause(true);
        // Unexpected inbound data must not mask EOF or block reconnection.
        await client.GetStream().WriteAsync(new byte[]{65,66,67});
    }
    using var next=new TcpClient(); await next.ConnectAsync(IPAddress.Loopback,port);
    await reconnected.Task.WaitAsync(TimeSpan.FromSeconds(2));
    Equal(true,e.State.Paused); Equal(0,e.State.Repeated);
    e.Pause(false); e.SetInterval(10);
    Equal(Convert.ToHexString(FrameEncoder.Encode(20000)),Convert.ToHexString(await ReadFrame(next.GetStream())));
});
await TestAsync("TCP manual changes and automatic resume preserve complete stable frames", async () => {
    var e=new WeightEngine(); e.SetInterval(30); e.Manual(20000);
    int port=FreePort(); await using var server=new TcpScaleServer(e,port);
    var atSeven=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
    server.Sent+=_=> { if(e.State.Automatic && e.State.Repeated==7) { e.Pause(true); atSeven.TrySetResult(); } };
    server.Start();
    using var client=new TcpClient(); await client.ConnectAsync(IPAddress.Loopback,port);
    for(int i=0;i<3;i++) Equal(Convert.ToHexString(FrameEncoder.Encode(20000)),Convert.ToHexString(await ReadFrame(client.GetStream())));
    e.Manual(12000); bool changed=false;
    for(int i=0;i<5;i++) {
        var frame=Convert.ToHexString(await ReadFrame(client.GetStream()));
        if(frame==Convert.ToHexString(FrameEncoder.Encode(12000))) { changed=true; break; }
        Equal(Convert.ToHexString(FrameEncoder.Encode(20000)),frame);
    }
    Equal(true,changed);
    e.Pause(true); await Task.Delay(100);
    // Drain complete frames written before pause; then restart at an exact, known boundary.
    while(client.Available>=14) await ReadFrame(client.GetStream());
    e.StartAutomatic(new(100,60,20,10,30)); e.Pause(false);
    for(int i=0;i<7;i++) Equal(Convert.ToHexString(FrameEncoder.Encode(100)),Convert.ToHexString(await ReadFrame(client.GetStream())));
    await atSeven.Task.WaitAsync(TimeSpan.FromSeconds(2)); await Task.Delay(80); Equal(7,e.State.Repeated); Equal(0,client.Available);
    e.Pause(false);
    for(int i=0;i<3;i++) Equal(Convert.ToHexString(FrameEncoder.Encode(100)),Convert.ToHexString(await ReadFrame(client.GetStream())));
    Equal(Convert.ToHexString(FrameEncoder.Encode(80)),Convert.ToHexString(await ReadFrame(client.GetStream())));
});

Console.WriteLine($"{passed} passed; {failed} failed");
return failed==0 ? 0 : 1;
