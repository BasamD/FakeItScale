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
                try
                {
                    using var stream = client.GetStream();
                    while(!ct.IsCancellationRequested)
                    {
                        if(client.Client.Poll(0, SelectMode.SelectRead) && client.Client.Available == 0) break;
                        var ticket = engine.Next();
                        if(ticket != null)
                        {
                            using var writeTimeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
                            writeTimeout.CancelAfter(TimeSpan.FromSeconds(3));
                            await stream.WriteAsync(ticket.Bytes, writeTimeout.Token);
                            engine.Commit(ticket);
                            Sent?.Invoke(ticket);
                            Status?.Invoke("Streaming");
                        }
                        else Status?.Invoke("Paused");
                        await Task.Delay(engine.Settings.IntervalMs, ct);
                    }
                }
                catch(Exception ex) when(ex is IOException or SocketException or OperationCanceledException)
                { if(!ct.IsCancellationRequested) Status?.Invoke("Disconnected: " + ex.Message); }
                Status?.Invoke("Disconnected");
            }
        }
        catch(OperationCanceledException) when(ct.IsCancellationRequested) { }
        catch(SocketException) when(ct.IsCancellationRequested) { }
        catch(Exception ex) { Status?.Invoke("Error: " + ex.Message); }
    }
    public async ValueTask DisposeAsync()
    {
        stop.Cancel(); listener.Stop();
        if(task != null) await task;
        stop.Dispose();
    }
}
