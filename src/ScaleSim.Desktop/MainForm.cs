using System.IO.Ports;
using System.Text.Json;
using ScaleSim.Core;
namespace ScaleSim.Desktop;

public sealed class MainForm : Form
{
    readonly WeightEngine engine = new();
    readonly ComboBox transport = Choice("TCP/IP", "Virtual COM");
    readonly ComboBox mode = Choice("Manual", "Automatic");
    readonly ComboBox ports = new() { Width = 140, DropDownStyle = ComboBoxStyle.DropDownList };
    readonly CheckBox lan = new() { Text = "Allow LAN (all IPv4 interfaces)", AutoSize = true };
    readonly CheckBox hex = new() { Text = "Show hexadecimal", AutoSize = true };
    readonly TextBox manual = new() { Text = "20000", Width = 110 };
    readonly TextBox peer = new() { Width = 100, PlaceholderText = "e.g. COM11" };
    readonly NumericUpDown port = Number(10001, 1, 65535);
    readonly NumericUpDown start = Number(20000, 0, 99999), minimum = Number(0, 0, 99999), decrement = Number(20, 1, 99999), repeats = Number(10, 10, 1000), interval = Number(500, 10, 60000);
    readonly Label weight = new() { Font = new Font("Segoe UI", 30, FontStyle.Bold), AutoSize = true };
    readonly Label status = new() { Text = "Stopped", AutoSize = true, MaximumSize = new Size(820, 0) };
    readonly Label counts = new() { AutoSize = true };
    readonly ListBox log = new() { Dock = DockStyle.Fill, HorizontalScrollbar = true, Font = new Font("Consolas", 9) };
    readonly Button connect = new() { Text = "Start connection", AutoSize = true };
    readonly Button disconnect = new() { Text = "Stop connection", AutoSize = true, Enabled = false };
    readonly List<Control> locked = new();
    readonly System.Windows.Forms.Timer refresh = new() { Interval = 100 };
    IAsyncDisposable? active;
    bool busy, closing;
    readonly string settingsFile = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ScaleSim", "settings.json");
    static ComboBox Choice(params string[] items) { var c = new ComboBox { Width = 130, DropDownStyle = ComboBoxStyle.DropDownList }; c.Items.AddRange(items); c.SelectedIndex = 0; return c; }
    static NumericUpDown Number(int value, int min, int max) => new() { Maximum = max, Minimum = min, Value = value, Width = 100 };
    public MainForm()
    {
        Text = "ScaleSim — SIMULATION / TEST ONLY"; Width = 960; Height = 800; MinimumSize = new Size(850, 720);
        Font = new Font("Segoe UI", 10); AutoScaleMode = AutoScaleMode.Dpi;
        var root = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(16), ColumnCount = 1, RowCount = 9 };
        root.RowStyles.Clear(); for(int i=0;i<8;i++) root.RowStyles.Add(new RowStyle(SizeType.AutoSize)); root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        Controls.Add(root);
        root.Controls.Add(new Label { Text = "SIMULATION / TEST ONLY", ForeColor = Color.DarkRed, AutoSize = true, Font = new Font(Font, FontStyle.Bold) });
        root.Controls.Add(Row(weight, mode, counts));
        root.Controls.Add(Row(new Label { Text = "Connection", AutoSize = true }, transport, new Label { Text = "TCP port", AutoSize = true }, port, lan));
        root.Controls.Add(Row(new Label { Text = "Simulator COM", AutoSize = true }, ports, Button("Refresh ports", RefreshPorts), new Label { Text = "WasteWORKS COM (operator supplied)", AutoSize = true }, peer, Button("COM setup", SerialHelp)));
        root.Controls.Add(Row(connect, disconnect, status));
        root.Controls.Add(Row(new Label { Text = "Manual weight (lb)", AutoSize = true }, manual, Button("Apply Weight", ApplyManual), Button("20,000 lb", () => Preset(20000)), Button("12,000 lb", () => Preset(12000)), Button("0 lb", () => Preset(0))));
        root.Controls.Add(Row(Field("Start", start), Field("Minimum", minimum), Field("Decrement", decrement), Field("Sends / weight", repeats), Field("Interval ms", interval), Button("Apply / Reset Cycle", ApplyAuto)));
        root.Controls.Add(Row(Button("Start fresh", () => { engine.Reset(); engine.Pause(false); }), Button("Pause", () => engine.Pause(true)), Button("Resume", () => engine.Pause(false)), Button("Stop output", () => { engine.Pause(true); engine.Reset(); }), hex));
        root.Controls.Add(log);
        locked.AddRange(new Control[] { transport, port, lan, ports, peer });
        connect.Click += async (_,_) => await StartConnection();
        disconnect.Click += async (_,_) => await StopConnection();
        mode.SelectedIndexChanged += (_,_) => { if(mode.SelectedIndex == 1) ApplyAuto(); else ApplyManual(); };
        refresh.Tick += (_,_) => { var s=engine.State; weight.Text = $"{s.Weight:N0} lb"; counts.Text = $"Completed: {s.Completed:N0}   Hold: {s.Repeated} / {s.Required}"; };
        FormClosing += CloseApp;
        RefreshPorts(); LoadSettings(); refresh.Start();
    }
    static FlowLayoutPanel Row(params Control[] controls) { var p = new FlowLayoutPanel { AutoSize = true, Dock = DockStyle.Top, WrapContents = true, Padding = new Padding(0, 5, 0, 5) }; p.Controls.AddRange(controls); return p; }
    static Control Field(string title, Control value) => Row(new Label { Text = title, AutoSize = true }, value);
    static Button Button(string text, Action action) { var b = new Button { Text = text, AutoSize = true }; b.Click += (_,_) => action(); return b; }
    void Ui(Action action) { if(!IsDisposed && IsHandleCreated) { if(InvokeRequired) BeginInvoke(action); else action(); } }
    void Report(string text) => Ui(() => {
        if(status.Text == text) return;
        status.Text = text;
        if(text.StartsWith("Error") || text.StartsWith("Disconnected")) {
            log.Items.Add($"{DateTimeOffset.Now:HH:mm:ss.fff zzz}  {text}");
            while(log.Items.Count > 500) log.Items.RemoveAt(0);
        }
    });
    void Sent(FrameTicket ticket) => Ui(() => {
        var frame = hex.Checked ? BitConverter.ToString(ticket.Bytes).Replace("-", " ") : $"[2]   {ticket.Weight:D5}LG [13][10]";
        log.Items.Add($"{DateTimeOffset.Now:HH:mm:ss.fff zzz}  {frame}");
        while(log.Items.Count > 500) log.Items.RemoveAt(0);
        log.TopIndex = log.Items.Count - 1;
    });
    void Guard(Action action) { try { action(); } catch(Exception ex) { MessageBox.Show(this, ex.Message, "ScaleSim", MessageBoxButtons.OK, MessageBoxIcon.Warning); } }
    void ApplyManual() => Guard(() => { int value=FrameEncoder.Parse(manual.Text); engine.Manual(value); if(mode.SelectedIndex != 0) mode.SelectedIndex=0; });
    void Preset(int value) { manual.Text = value.ToString(); ApplyManual(); }
    void ApplyAuto() => Guard(() => {
        engine.Configure(new((int)start.Value, (int)minimum.Value, (int)decrement.Value, (int)repeats.Value, (int)interval.Value));
        engine.Automatic(); if(mode.SelectedIndex != 1) mode.SelectedIndex=1;
    });
    void RefreshPorts() => Guard(() => { var selected = ports.Text; ports.Items.Clear(); ports.Items.AddRange(SerialPort.GetPortNames().Order().ToArray()); if(ports.Items.Contains(selected)) ports.SelectedItem=selected; else if(ports.Items.Count>0) ports.SelectedIndex=0; });
    void SerialHelp() => MessageBox.Show(this,
        "Install a Windows 11-compatible, signed virtual serial-port driver from a trusted vendor (for example a vendor-supported virtual null-modem product). Follow the vendor's official instructions to create an unused COM pair. Verify both ports in Device Manager.\n\nSelect the simulator endpoint here; type the paired WasteWORKS endpoint for your reference. ScaleSim cannot infer the peer from Windows serial enumeration and does not create or install drivers. Configure WasteWORKS for 9600 baud, 8 data bits, no parity, 1 stop bit, no flow control.\n\nDo not disable driver signature enforcement or reuse physical COM ports. A successful write does not prove WasteWORKS receipt.", "Virtual COM setup");
    async Task StartConnection()
    {
        if(active != null || busy) return;
        busy=true; connect.Enabled=false;
        try {
            if(mode.SelectedIndex == 1) { var s = new AutoSettings((int)start.Value,(int)minimum.Value,(int)decrement.Value,(int)repeats.Value,(int)interval.Value); s.Validate(); engine.Configure(s); engine.Automatic(); }
            else { var value=FrameEncoder.Parse(manual.Text); engine.Configure(new(IntervalMs:(int)interval.Value)); engine.Manual(value); }
            engine.Pause(false);
            if(transport.SelectedIndex == 0) {
                var server = new TcpScaleServer(engine, (int)port.Value, lan.Checked);
                server.Status += Report; server.Sent += Sent;
                active=server; server.Start(); Report(lan.Checked ? "Listening on all IPv4 interfaces" : "Listening on 127.0.0.1");
            } else {
                if(string.IsNullOrEmpty(ports.Text)) throw new ArgumentException("No COM port selected. Use COM setup for driver instructions.");
                var serial = new SerialTransport(ports.Text, engine, Sent, Report); active=serial; await serial.Start(); Report("Connected: " + ports.Text);
            }
            foreach(var c in locked) c.Enabled=false;
            disconnect.Enabled=true;
        } catch(Exception ex) {
            if(active != null) { await active.DisposeAsync(); active=null; }
            Report("Error: " + ex.Message); connect.Enabled=true;
        } finally { busy=false; }
    }
    async Task StopConnection()
    {
        if(busy) return;
        busy=true; disconnect.Enabled=false;
        try { if(active != null) await active.DisposeAsync(); }
        catch(Exception ex) { Report("Error stopping: " + ex.Message); }
        finally { active=null; busy=false; connect.Enabled=true; foreach(var c in locked) c.Enabled=true; Report("Stopped"); }
    }
    async void CloseApp(object? sender, FormClosingEventArgs e)
    {
        if(closing) return;
        e.Cancel=true; if(busy) return;
        Enabled=false; await StopConnection(); SaveSettings(); refresh.Stop(); closing=true; Close();
    }
    sealed record Preferences(int Port, bool Lan, string Com, string Peer, int Mode, string Manual, AutoSettings Auto, bool Hex, int Transport);
    void SaveSettings()
    {
        try {
            Directory.CreateDirectory(Path.GetDirectoryName(settingsFile)!);
            var p = new Preferences((int)port.Value,lan.Checked,ports.Text,peer.Text,mode.SelectedIndex,manual.Text,new((int)start.Value,(int)minimum.Value,(int)decrement.Value,(int)repeats.Value,(int)interval.Value),hex.Checked,transport.SelectedIndex);
            File.WriteAllText(settingsFile + ".tmp", JsonSerializer.Serialize(p)); File.Move(settingsFile + ".tmp", settingsFile, true);
        } catch(Exception ex) { MessageBox.Show(this,"Could not save preferences: " + ex.Message); }
    }
    void LoadSettings()
    {
        try {
            if(!File.Exists(settingsFile)) return;
            var p = JsonSerializer.Deserialize<Preferences>(File.ReadAllText(settingsFile)); if(p == null) return;
            p.Auto.Validate(); FrameEncoder.Parse(p.Manual);
            if(p.Port is <1 or >65535 || p.Mode is <0 or >1 || p.Transport is <0 or >1) throw new ArgumentException("Invalid saved settings.");
            port.Value=p.Port; lan.Checked=p.Lan; peer.Text=p.Peer; if(ports.Items.Contains(p.Com)) ports.SelectedItem=p.Com;
            start.Value=p.Auto.Start; minimum.Value=p.Auto.Minimum; decrement.Value=p.Auto.Decrement; repeats.Value=p.Auto.Repeats; interval.Value=p.Auto.IntervalMs;
            manual.Text=p.Manual; hex.Checked=p.Hex; transport.SelectedIndex=p.Transport;
            if(p.Mode==1) { mode.SelectedIndex=1; ApplyAuto(); } else ApplyManual();
        } catch(Exception ex) { Report("Saved settings ignored: " + ex.Message); }
    }
}
