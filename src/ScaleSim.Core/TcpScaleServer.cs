using System.Net;
using System.Net.Sockets;
namespace ScaleSim.Core;

public sealed class TcpScaleServer : IAsyncDisposable
{
    readonly WeightEngine engine;
    readonly TcpListener listener;
    readonly CancellationTokenSource stop = new();
    Task? task;
    public event Action<string>? Status;
    public event Action<FrameTicket>? Sent;
    public TcpScaleServer(WeightEngine engine, int port, bool lan = false)
    {
        if(port is < 1 or > 65535) throw new ArgumentOutOfRangeException(nameof(port));
        this.engine = engine;
        listener = new(lan ? IPAddress.Any : IPAddress.Loopback, port);
    }
    public void Start() { listener.Start(); task = Run(); }
    async Task Run()
    {
        var ct = stop.Token;
        try
        {
            while(!ct.IsCancellationRequested)
            {
                Status?.Invoke("Listening");
                using var client = await listener.AcceptTcpClientAsync(ct);
                client.NoDelay = true;
                engine.Reconnected();
                Status?.Invoke("Connected: " + client.Client.RemoteEndPoint);
                using var session = CancellationTokenSource.CreateLinkedTokenSource(ct);
                using var stream = client.GetStream();
                // Observe peer EOF independently of the frame interval, including while paused.
                var peer = WatchPeer(stream, session);
                try
                {
                    while(!session.IsCancellationRequested)
                    {
                        var ticket = engine.Next();
                        if(ticket != null)
                        {
                            using var writeTimeout = CancellationTokenSource.CreateLinkedTokenSource(session.Token);
                            writeTimeout.CancelAfter(TimeSpan.FromSeconds(3));
                            await stream.WriteAsync(ticket.Bytes, writeTimeout.Token);
                            engine.Commit(ticket);
                            Sent?.Invoke(ticket);
                            Status?.Invoke("Streaming");
                        }
                        else Status?.Invoke("Paused");
                        await FrameClock.Wait(engine, session.Token);
                    }
                }
                catch(Exception ex) when(ex is IOException or SocketException or OperationCanceledException)
                {
                    if(!ct.IsCancellationRequested && !session.IsCancellationRequested)
                        Status?.Invoke("Disconnected: " + ex.Message);
                }
                finally { session.Cancel(); await peer; }
                if(!ct.IsCancellationRequested) Status?.Invoke("Disconnected");
            }
        }
        catch(OperationCanceledException) when(ct.IsCancellationRequested) { }
        catch(SocketException) when(ct.IsCancellationRequested) { }
        catch(Exception ex) { Status?.Invoke("Error: " + ex.Message); }
    }
    static async Task WatchPeer(NetworkStream stream, CancellationTokenSource session)
    {
        try
        {
            var ignored = new byte[256];
            while(await stream.ReadAsync(ignored, session.Token) != 0) { }
        }
        catch(Exception ex) when(ex is IOException or SocketException or OperationCanceledException) { }
        finally { session.Cancel(); }
    }
    public async ValueTask DisposeAsync()
    {
        stop.Cancel(); listener.Stop();
        if(task != null) await task;
        stop.Dispose();
    }
}
