using System.IO.Ports;
using ScaleSim.Core;
namespace ScaleSim.Desktop;
sealed class SerialTransport : IAsyncDisposable
{
    readonly SerialPort port;
    readonly WeightEngine engine;
    readonly Action<FrameTicket> sent;
    readonly Action<string> status;
    readonly CancellationTokenSource stop = new();
    Task? task;
    public SerialTransport(string name, WeightEngine engine, Action<FrameTicket> sent, Action<string> status)
    {
        port = new(name, 9600, Parity.None, 8, StopBits.One) { Handshake = Handshake.None, WriteTimeout = 2000 };
        this.engine = engine; this.sent = sent; this.status = status;
    }
    public async Task Start()
    {
        await Task.Run(() => port.Open());
        engine.Reconnected();
        task = Task.Run(async () => {
            try {
                while(!stop.IsCancellationRequested) {
                    var ticket = engine.Next();
                    if(ticket != null) {
                        port.Write(ticket.Bytes, 0, ticket.Bytes.Length);
                        engine.Commit(ticket); sent(ticket); status("Streaming (serial write completed)");
                    } else status("Paused");
                    await Task.Delay(engine.Settings.IntervalMs, stop.Token);
                }
            }
            catch(OperationCanceledException) when(stop.IsCancellationRequested) { }
            catch(Exception ex) { status("Error: " + ex.Message); }
        });
    }
    public async ValueTask DisposeAsync()
    {
        stop.Cancel(); if(task != null) await task;
        port.Dispose(); stop.Dispose();
    }
}
