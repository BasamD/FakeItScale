using System.IO.Ports;
using System.Text.Json;
using ScaleSim.Core;
namespace ScaleSim.Desktop;

public sealed class MainForm : Form
{
    readonly WeightEngine engine = new();
    readonly ActivityMonitor activity = new();
    readonly ComboBox transport = Choice("TCP/IP", "Virtual COM");
    readonly ComboBox mode = Choice("Manual", "Automatic");
    readonly ComboBox ports = new() { Width = 132, DropDownStyle = ComboBoxStyle.DropDownList };
    readonly CheckBox lan = new() { Text = "Allow LAN connections", AutoSize = true };
    readonly CheckBox hex = new() { Text = "Hex bytes", AutoSize = true };
    readonly CheckBox reduceMotion = new() { Text = "Reduce motion", AutoSize = true };
    readonly CheckBox showLog = new() { Text = "Show transmission log", AutoSize = true };
    readonly TextBox manual = new() { Text = "20000", Width = 124, Font = Theme.Font(15, true) };
    readonly TextBox peer = new() { Width = 124, PlaceholderText = "e.g. COM11" };
    readonly TextBox address = new() { Text = "127.0.0.1", Width = 134, ReadOnly = true };
    readonly NumericUpDown port = Number(10001, 1, 65535);
    readonly NumericUpDown start = Number(20000, 0, 99999), minimum = Number(0, 0, 99999), decrement = Number(20, 1, 99999), repeats = Number(10, 10, 1000), interval = Number(500, 10, 60000);
    readonly Label weight = Theme.Label("20,000 lb", 34, true);
    readonly Label badge = Theme.Label("Stopped", 11, true);
    readonly Label frames = Theme.Label("0", 19, true);
    readonly Label lastSend = Theme.Label("—", 12, true);
    readonly Label errors = Theme.Label("0", 19, true);
    readonly Label hold = Theme.Label("Manual · fixed weight", 10);
    readonly Label endpoint = Theme.Label("Start the listener, then connect WasteWORKS.", 9, color: Theme.Muted);
    readonly Label notice = Theme.Label("Ready. 1 Connect → 2 Choose a weight → 3 Stream.", 10, color: Theme.Muted);
    readonly Label sceneCaption = Theme.Label("Waiting for a connection", 10, true);
    readonly Label autoSummary = Theme.Label("", 9, color: Theme.Muted);
    readonly ProgressBar holdProgress = new() { Maximum = 1000, Height = 10, Style = ProgressBarStyle.Continuous };
    readonly ListBox log = new() { Height = 160, HorizontalScrollbar = true, IntegralHeight = false, Font = new Font("Consolas", 9), BorderStyle = BorderStyle.None };
    readonly WeightDial dial = new();
    readonly TruckScene scene = new();
    readonly Button connect = Button("Start connection", primary: true);
    readonly Button disconnect = Button("Stop connection");
    readonly Button fresh = Button("Start fresh", primary: true), pause = Button("Pause"), resume = Button("Resume"), stopOutput = Button("Stop output");
    readonly StackLayout manualPanel = new();
    readonly StackLayout autoPanel = new();
    readonly StackLayout tcpPanel = Column(), serialPanel = Column();
    readonly Card logCard = new() { Visible = false };
    readonly List<Control> connectionFields = new();
    readonly System.Windows.Forms.Timer refresh = new() { Interval = 100 };
    readonly bool persistPreferences;
    readonly string settingsFile = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ScaleSim", "settings.json");
    readonly object statusGate = new();
    string connectionStatus = "Stopped", client = "";
    bool connected, busy, closing, changingControls;
    IAsyncDisposable? active;
    AutoSettings appliedAuto = new();
    int manualWeight = 20000;
    ResponsiveColumns workspace = null!;
    readonly Panel scroll = new() { Dock = DockStyle.Fill, AutoScroll = true };
    readonly StackLayout root = new() { Spacing = 12 };
    readonly ToolTip help = new() { AutoPopDelay = 15000 };
    Card connectionCard = null!, weightCard = null!;
    bool layoutDirty = true, arranging;
    long writesWhenConnected;
    string uiCheckFolder = "";

    static ComboBox Choice(params string[] items) { var c = new ComboBox { Width = 145, DropDownStyle = ComboBoxStyle.DropDownList }; c.Items.AddRange(items); c.SelectedIndex = 0; return c; }
    static NumericUpDown Number(int value, int min, int max) => new() { Maximum = max, Minimum = min, Value = value, Width = 116, ThousandsSeparator = true };
    static Button Button(string text, Action? action = null, bool primary = false)
    {
        var b = new Button { Text = text, AutoSize = true, MinimumSize = new Size(80, 34), Padding = new Padding(10, 2, 10, 2), FlatStyle = FlatStyle.Flat, Cursor = Cursors.Hand, Margin = new Padding(0, 0, 8, 6) };
        b.FlatAppearance.BorderColor = primary ? Theme.Accent : Theme.Border;
        b.BackColor = primary ? Theme.Accent : Color.White; b.ForeColor = primary ? Color.White : Theme.Ink;
        if(primary) b.EnabledChanged+=(_,_)=> {
            b.BackColor=b.Enabled?Theme.Accent:Color.FromArgb(229,236,240);
            b.ForeColor=b.Enabled?Color.White:Theme.Muted;
            b.FlatAppearance.BorderColor=b.Enabled?Theme.Accent:Theme.Border;
        };
        if(action != null) b.Click += (_,_) => action(); return b;
    }
    static WrapLayout Row(params Control[] controls) => new(controls);
    static StackLayout Column(params Control[] controls) => new(controls);
    static Control Field(string title, Control value, int width=144) => new FieldLayout(title,value,width);

    public MainForm(bool persistPreferences = true)
    {
        this.persistPreferences = persistPreferences;
        Text = "ScaleSim 1.2 — SIMULATION / TEST ONLY";
        Font = Theme.Font(); ForeColor = Theme.Ink; BackColor = Theme.Background;
        AutoScaleMode = AutoScaleMode.Dpi;
        ClientSize = new Size(1160, 900); MinimumSize = new Size(640, 580);
        StartPosition = FormStartPosition.CenterScreen;
        var screen = Screen.FromPoint(Cursor.Position).WorkingArea;
        Size = new Size(Math.Min(Width,screen.Width-20), Math.Min(Height,screen.Height-30));
        scroll.Controls.Add(root); Controls.Add(scroll);
        root.Controls.Add(Row(Theme.Label("ScaleSim",19,true),Theme.Label("1.2",10,color:Theme.Muted),Theme.Label("SIMULATION / TEST ONLY",9,true,Color.FromArgb(150,65,27))));
        root.Controls.Add(BuildDashboard());
        connectionCard=BuildConnection(); weightCard=BuildWeightControls();
        workspace=new ResponsiveColumns(connectionCard,weightCard,350,510);
        root.Controls.Add(workspace);
        root.Controls.Add(Row(Theme.Label("3  Stream",11,true),fresh,pause,resume,stopOutput));
        root.Controls.Add(BuildScene());
        root.Controls.Add(Row(showLog,hex,Button("Clear log",()=>{ log.Items.Clear(); activity.Drain(); })));
        logCard.Controls.Add(log); root.Controls.Add(logCard); root.Controls.Add(notice);
        help.SetToolTip(frames,"Successful complete writes since Start connection. Resets only when a new connection session starts. Local writes do not confirm WasteWORKS receipt.");
        help.SetToolTip(lastSend,"Time and weight of the most recent successful local write in this session. Retained after pause, disconnect, or reset until another frame is written.");
        help.SetToolTip(errors,"Reported connection/open/write failures in this session. Clean disconnects and input validation are not errors.");
        help.SetToolTip(weight,"Current weight-generator state. Last write shows what was actually transmitted, which can differ after a reset or manual edit.");
        help.SetToolTip(hold,"Successful writes at the current automatic weight. Reconnecting starts a new complete stable window.");
        SizeChanged+=(_,_)=> { layoutDirty=true; Reflow(); };
        DpiChanged+=(_,_)=> { layoutDirty=true; Reflow(); };
        Shown+=(_,_)=> { layoutDirty=true; Reflow(); };
        WatchLayout(root);
        connectionFields.AddRange(new Control[]{transport,port,lan,ports,peer});
        connect.Click += async (_,_)=>await StartConnection(); disconnect.Click += async (_,_)=>await StopConnection();
        fresh.Click += (_,_)=> { ResetFlowBaseline(); engine.Reset(); engine.Pause(false); SetNotice("Fresh session. Waiting for the next scheduled frame."); UpdateView(); };
        pause.Click += (_,_)=> { engine.Pause(true); SetNotice("Paused. Weight and stable-send count are preserved."); UpdateView(); };
        resume.Click += (_,_)=> { ResetFlowBaseline(); engine.Pause(false); SetNotice("Resumed. The next scheduled frame continues the sequence."); UpdateView(); };
        stopOutput.Click += (_,_)=> { engine.Pause(true); engine.Reset(); SetNotice("Output stopped and session reset. Start fresh to send again."); UpdateView(); };
        transport.SelectedIndexChanged += (_,_)=>ShowTransport();
        lan.CheckedChanged += (_,_)=>address.Text=lan.Checked ? "0.0.0.0" : "127.0.0.1";
        mode.SelectedIndexChanged += (_,_)=> { if(!changingControls) ApplySelectedMode(); };
        dial.ValueChanged += (_,_)=> { if(!changingControls && !engine.State.Automatic) ApplyManualValue(dial.Value); };
        manual.KeyDown += (_,e)=> { if(e.KeyCode==Keys.Enter) { ApplyManual(); e.Handled=true; e.SuppressKeyPress=true; } };
        showLog.CheckedChanged += (_,_)=> { logCard.Visible=showLog.Checked; layoutDirty=true; Reflow(); };
        refresh.Tick += (_,_)=>UpdateView(); FormClosing += CloseApp;
        RefreshPorts(); if(persistPreferences) LoadSettings();
        ShowTransport(); ShowMode(); SetConnectionControls(); UpdateView(); refresh.Start();
    }
    Control BuildDashboard()
    {
        var card=new Card();
        var weightGroup=Column(Theme.Label("CURRENT SIMULATED WEIGHT",9,true,Theme.Muted),weight,hold);
        var statistics=Row(
            new FieldLayout("Frames written",frames,138),
            new FieldLayout("Last write",lastSend,168),
            new FieldLayout("Connection / write errors",errors,170));
        card.Controls.Add(new ResponsiveColumns(weightGroup,Column(statistics,badge),330,370));
        return card;
    }
    Card BuildConnection()
    {
        var card=new Card();
        tcpPanel.Controls.AddRange(new Control[]{Row(Field("Listener address",address,156),Field("TCP port",port,124)),lan});
        serialPanel.Controls.AddRange(new Control[]{Row(Field("Simulator port",ports,144),Button("Refresh",RefreshPorts)),Field("WasteWORKS paired COM (your reference)",peer,274),Button("Driver setup",SerialHelp)});
        serialPanel.Visible=false;
        var applyInterval=Button("Apply",()=>Guard(()=>{ engine.SetInterval((int)interval.Value); appliedAuto=appliedAuto with { IntervalMs=(int)interval.Value }; SetNotice("Interval applied. Current weight and repeat count preserved."); }));
        card.Controls.AddRange(new Control[]{Theme.Label("1  Connection",12,true),transport,tcpPanel,serialPanel,
            Row(Field("Frame interval (ms)",interval,154),applyInterval),Row(connect,disconnect),endpoint});
        return card;
    }
    Card BuildWeightControls()
    {
        var card=new Card();
        var detail=Column(Theme.Label("Exact weight (lb)",10,true),Row(manual,Button("Apply",ApplyManual,true)),
            Row(Button("20,000",()=>Preset(20000)),Button("12,000",()=>Preset(12000)),Button("Zero",()=>Preset(0))),
            Row(Button("−20 lb",()=>Nudge(-20)),Button("+20 lb",()=>Nudge(20))),
            Theme.Label("Dial changes apply live. Arrow keys: 20 lb. Page keys: 1,000 lb. Exact entry applies when you click Apply.",9,color:Theme.Muted));
        manualPanel.Controls.Add(new ResponsiveColumns(dial,detail,224,280));
        autoPanel.Controls.AddRange(new Control[]{Theme.Label("Hold each weight for a stable capture window.",10,true),
            Row(Field("Start (lb)",start,140),Field("Minimum (lb)",minimum,140),Field("Decrement (lb)",decrement,140)),
            Row(Field("Sends per weight (10–1,000)",repeats,208),Button("Apply and restart",ApplyAuto,true)),autoSummary,
            Theme.Label("Changes apply when you click Apply and restart. Pause keeps your place; reconnect gives a full hold window.",9,color:Theme.Muted),holdProgress});
        card.Controls.AddRange(new Control[]{Row(Theme.Label("2  Set the weight",12,true),mode),manualPanel,autoPanel});
        return card;
    }
    Control BuildScene()
    {
        var card=new Card();
        card.Controls.AddRange(new Control[]{Row(sceneCaption,reduceMotion),scene,
            Theme.Label("Truck motion shows local feed activity. Confirm captured weights in WasteWORKS.",9,color:Theme.Muted)});
        return card;
    }
    void WatchLayout(Control control)
    {
        control.TextChanged+=(_,_)=>layoutDirty=true;
        control.VisibleChanged+=(_,_)=>layoutDirty=true;
        control.FontChanged+=(_,_)=>layoutDirty=true;
        foreach(Control child in control.Controls) WatchLayout(child);
    }
    void Reflow()
    {
        if(!layoutDirty || arranging || !IsHandleCreated || !Visible) return;
        arranging=true;
        try {
            int pad=LayoutSize.Px(this,18);
            // Reserve scrollbar width consistently so crossing the height boundary cannot
            // oscillate between one and two columns.
            int width=Math.Max(1,scroll.ClientSize.Width-pad*2-SystemInformation.VerticalScrollBarWidth);
            var measured=root.GetPreferredSize(new Size(width,0));
            var position=scroll.AutoScrollPosition;
            root.SetBounds(pad+position.X,pad+position.Y,width,measured.Height);
            root.PerformLayout();
            scroll.AutoScrollMinSize=new Size(0,measured.Height+pad*2);
            layoutDirty=false;
        } finally { arranging=false; }
    }
    void ShowTransport()
    {
        bool tcp=transport.SelectedIndex==0; tcpPanel.Visible=tcp; serialPanel.Visible=!tcp;
        layoutDirty=true; Reflow();
    }
    void ShowMode()
    {
        bool automatic=engine.State.Automatic; autoPanel.Visible=automatic; manualPanel.Visible=!automatic;
        layoutDirty=true; Reflow();
    }
    void SelectMode(bool automatic)
    {
        changingControls=true; mode.SelectedIndex=automatic?1:0; changingControls=false; ShowMode();
    }
    bool Guard(Action action)
    {
        try { action(); return true; }
        catch(Exception ex) when(ex is ArgumentException or IOException or UnauthorizedAccessException or System.Security.SecurityException)
        { SetNotice(ex.Message,true); return false; }
    }
    void ApplySelectedMode()
    {
        if(mode.SelectedIndex==1) ApplyAuto(); else ApplyManual();
        // A rejected draft must never leave the selector disagreeing with the engine.
        SelectMode(engine.State.Automatic); UpdateView();
    }
    void ApplyManual() => Guard(()=>ApplyManualValue(FrameEncoder.Parse(manual.Text)));
    void ApplyManualValue(int value)
    {
        engine.Manual(value); manualWeight=value; changingControls=true; dial.Value=value; manual.Text=value.ToString(); changingControls=false;
        SelectMode(false); SetNotice($"Manual weight set to {value:N0} lb. Next scheduled frame uses this weight."); UpdateView();
    }
    void Preset(int value) => ApplyManualValue(value);
    void Nudge(int amount) => ApplyManualValue(Math.Clamp(engine.State.Weight+amount,0,99999));
    AutoSettings DraftAuto() => new((int)start.Value,(int)minimum.Value,(int)decrement.Value,(int)repeats.Value,(int)interval.Value);
    void ApplyAuto()
    {
        if(!Guard(()=>{ var draft=DraftAuto(); engine.StartAutomatic(draft); appliedAuto=draft; SelectMode(true); SetNotice("Automatic cycle restarted with the settings shown."); })) SelectMode(engine.State.Automatic);
        UpdateView();
    }
    void SetNotice(string text,bool error=false) { notice.Text=text; notice.ForeColor=error?Color.FromArgb(169,52,40):Theme.Muted; }
    void ResetFlowBaseline() { lock(statusGate) writesWhenConnected=activity.Snapshot.Writes; }
    void Report(string text)
    {
        lock(statusGate)
        {
            bool error=text.StartsWith("Error:") || text.StartsWith("Disconnected:");
            if(connectionStatus==text && !error) return;
            connectionStatus=text;
            if(text.StartsWith("Connected:")) { connected=true; client=text[11..]; writesWhenConnected=activity.Snapshot.Writes; }
            else if(text.StartsWith("Streaming") || text=="Paused") connected=true;
            else if(text=="Listening" || text=="Stopped" || text.StartsWith("Disconnected") || text.StartsWith("Error:")) { connected=false; client=""; }
            activity.Notice(text,error);
        }
    }
    void UpdateView()
    {
        if(IsDisposed || closing) return;
        string stateText,remote; bool hasClient; long connectionBaseline;
        lock(statusGate) { stateText=connectionStatus; remote=client; hasClient=connected; connectionBaseline=writesWhenConnected; }
        var state=engine.State; var stats=activity.Snapshot; var settings=engine.Settings;
        weight.Text=$"{state.Weight:N0} lb";
        frames.Text=stats.Writes.ToString("N0"); errors.Text=stats.Errors.ToString("N0");
        lastSend.Text=stats.LastWrite is {} time?$"{time:HH:mm:ss}\n{stats.LastFrame!.Weight:N0} lb":"No frames yet";
        hold.Text=state.Automatic?$"Stable window  {Math.Min(state.Repeated,state.Required)} / {state.Required} sends":"Manual · holds until you change it";
        holdProgress.Value=(int)Math.Clamp((long)state.Repeated*1000/state.Required,0,1000);
        autoSummary.Text=$"Applied: {settings.Repeats} sends × {settings.IntervalMs:N0} ms ≈ {settings.Repeats*settings.IntervalMs/1000d:0.##} sec per weight";
        bool flowActive=active!=null && hasClient && !state.Paused && stats.Writes>connectionBaseline && stats.HasRecentWrite(settings.IntervalMs);
        badge.Text=stateText.StartsWith("Error:")?"Error":active==null?"Stopped":state.Paused?"Paused":flowActive?"Feed active":hasClient?"Connected · waiting for data":"Listening · no client";
        badge.ForeColor=badge.Text=="Error"?Color.Firebrick:flowActive?Theme.Accent:Theme.Muted;
        endpoint.Text=hasClient?$"{(transport.SelectedIndex==0?"TCP client":"Serial port")}: {remote}\n{stats.Bytes:N0} bytes written this session":active!=null?"Waiting for a client. Automatic progression is frozen.":"Start the connection, then connect WasteWORKS.";
        fresh.Enabled=active!=null&&!busy; pause.Enabled=active!=null&&!busy&&!state.Paused; resume.Enabled=active!=null&&!busy&&state.Paused; stopOutput.Enabled=active!=null&&!busy;
        sceneCaption.Text=active==null?"Feed stopped":state.Paused?"Paused · trucks stopped":flowActive?"Feed active · trucks moving":hasClient?"Waiting for data · trucks stopped":"Waiting for a client · trucks stopped";
        if(reduceMotion.Checked && flowActive) sceneCaption.Text="Feed active · motion reduced";
        scene.SetFlow(flowActive,reduceMotion.Checked);
        var entries=activity.Drain();
        if(entries.Length>0)
        {
            log.BeginUpdate();
            foreach(var entry in entries)
            {
                string message=entry.Frame is {} f ? hex.Checked?BitConverter.ToString(f.Bytes).Replace("-"," "):$"[2]   {f.Weight:D5}LG [13][10]":entry.Message;
                log.Items.Add($"{entry.At:HH:mm:ss.fff}  {message}");
                if(entry.Message.StartsWith("Error:") || entry.Message.StartsWith("Disconnected:")) SetNotice(entry.Message,true);
            }
            while(log.Items.Count>500) log.Items.RemoveAt(0);
            if(log.Items.Count>0) log.TopIndex=log.Items.Count-1;
            log.EndUpdate();
        }
        Reflow();
    }
    void SetConnectionControls()
    {
        foreach(var field in connectionFields) field.Enabled=active==null&&!busy;
        connect.Enabled=active==null&&!busy; disconnect.Enabled=active!=null&&!busy;
    }
    async Task StartConnection()
    {
        if(active!=null||busy) return;
        // Validate every draft before touching the current session or opening a transport.
        AutoSettings? auto=null; int manualValue=0;
        if(!Guard(()=>{ if(engine.State.Automatic) { auto=DraftAuto(); auto.Validate(); } else manualValue=FrameEncoder.Parse(manual.Text); })) return;
        busy=true; SetConnectionControls();
        try
        {
            if(auto!=null) { engine.StartAutomatic(auto); appliedAuto=auto; }
            else { engine.SetInterval((int)interval.Value); appliedAuto=appliedAuto with { IntervalMs=(int)interval.Value }; engine.Manual(manualValue); manualWeight=manualValue; engine.Reset(); }
            engine.Pause(false); activity.Reset(); log.Items.Clear();
            if(transport.SelectedIndex==0)
            {
                var server=new TcpScaleServer(engine,(int)port.Value,lan.Checked);
                server.Status+=Report; server.Sent+=activity.Sent; active=server; server.Start();
            }
            else
            {
                if(string.IsNullOrEmpty(ports.Text)) throw new ArgumentException("No COM port selected. Open Driver setup for one-time instructions.");
                var serial=new SerialTransport(ports.Text,engine,activity.Sent,Report); active=serial; await serial.Start();
            }
            SetNotice(transport.SelectedIndex==0?$"Listening at {address.Text}:{port.Value}. Connect WasteWORKS as the client.":"Serial port open. Confirm received data in WasteWORKS.");
        }
        catch(Exception ex)
        {
            if(active!=null) { try { await active.DisposeAsync(); } catch { /* Preserve the original open error. */ } active=null; }
            Report("Error: "+ex.Message); SetNotice(ex.Message,true);
        }
        finally { busy=false; SetConnectionControls(); UpdateView(); }
    }
    async Task StopConnection()
    {
        if(busy) return;
        busy=true; SetConnectionControls();
        try { if(active!=null) await active.DisposeAsync(); }
        catch(Exception ex) { activity.Notice("Error: "+ex.Message,true); SetNotice(ex.Message,true); }
        finally { active=null; busy=false; Report("Stopped"); SetConnectionControls(); UpdateView(); }
    }
    async void CloseApp(object? sender,FormClosingEventArgs e)
    {
        if(closing) return;
        e.Cancel=true; if(busy) { SetNotice("Finishing the connection operation. Please close again in a moment."); return; }
        Enabled=false; await StopConnection(); if(persistPreferences) SaveSettings(); refresh.Stop(); closing=true; Close();
    }
    void RefreshPorts() => Guard(()=> { string selected=ports.Text; ports.Items.Clear(); ports.Items.AddRange(SerialPort.GetPortNames().Order().ToArray()); if(ports.Items.Contains(selected)) ports.SelectedItem=selected; else if(ports.Items.Count>0) ports.SelectedIndex=0; });
    void SerialHelp() => MessageBox.Show(this,"Install a trusted, signed Windows 11 virtual null-modem driver using its vendor's official setup utility. Create an unused pair such as COM10 / COM11 and verify both in Device Manager.\n\nChoose COM10 here and COM11 in WasteWORKS at 9600 baud, 8 data bits, no parity, 1 stop bit, no flow control. The paired port field is your reference; Windows enumeration cannot determine the partner.\n\nScaleSim does not install or create driver devices. Never disable signature enforcement or reuse physical COM ports. A successful write does not prove WasteWORKS capture.","Virtual COM setup");
    sealed record Preferences(int Port,bool Lan,string Com,string Peer,int Mode,string Manual,AutoSettings Auto,bool Hex,int Transport,bool ReduceMotion=false,bool ShowLog=false);
    void SaveSettings()
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(settingsFile)!);
            var p=new Preferences((int)port.Value,lan.Checked,ports.Text,peer.Text,engine.State.Automatic?1:0,manualWeight.ToString(),appliedAuto with { IntervalMs=engine.Settings.IntervalMs },hex.Checked,transport.SelectedIndex,reduceMotion.Checked,showLog.Checked);
            File.WriteAllText(settingsFile+".tmp",JsonSerializer.Serialize(p)); File.Move(settingsFile+".tmp",settingsFile,true);
        }
        catch(Exception ex) { MessageBox.Show(this,"Could not save preferences: "+ex.Message); }
    }
    void LoadSettings()
    {
        try
        {
            if(!File.Exists(settingsFile)) return;
            var p=JsonSerializer.Deserialize<Preferences>(File.ReadAllText(settingsFile)); if(p==null) return;
            p.Auto.Validate(); int manualValue=FrameEncoder.Parse(p.Manual);
            if(p.Port is <1 or >65535 || p.Mode is <0 or >1 || p.Transport is <0 or >1) throw new ArgumentException("Invalid saved settings.");
            port.Value=p.Port; lan.Checked=p.Lan; peer.Text=p.Peer; if(ports.Items.Contains(p.Com)) ports.SelectedItem=p.Com;
            start.Value=p.Auto.Start; minimum.Value=p.Auto.Minimum; decrement.Value=p.Auto.Decrement; repeats.Value=p.Auto.Repeats; interval.Value=p.Auto.IntervalMs;
            appliedAuto=p.Auto; manualWeight=manualValue; manual.Text=p.Manual; dial.Value=manualValue; hex.Checked=p.Hex; transport.SelectedIndex=p.Transport; reduceMotion.Checked=p.ReduceMotion; showLog.Checked=p.ShowLog;
            if(p.Mode==1) engine.StartAutomatic(p.Auto); else { engine.SetInterval(p.Auto.IntervalMs); engine.Manual(manualValue); }
            SelectMode(p.Mode==1);
        }
        catch(Exception ex) { SetNotice("Saved settings ignored: "+ex.Message,true); }
    }
    // Run by the packaged Windows executable on CI, after the window is shown.
    internal async Task RunUiChecks(string previewFolder)
    {
        void Check(bool ok,string message) { if(!ok) throw new InvalidOperationException(message); }
        refresh.Stop(); uiCheckFolder=previewFolder; Directory.CreateDirectory(uiCheckFolder);
        await CheckResponsiveLayouts();
        dial.Value=12000; Check(engine.State.Weight==12000 && manual.Text=="12000","Dial did not apply the manual weight.");
        Preset(20000); Check(dial.Value==20000 && engine.State.Weight==20000,"Preset and dial disagree.");
        ApplyAuto(); Check(engine.State.Automatic && autoPanel.Visible && !manualPanel.Visible,"Automatic controls did not activate.");
        manual.Text="invalid"; mode.SelectedIndex=0;
        Check(engine.State.Automatic && mode.SelectedIndex==1,"Invalid manual draft changed the active mode.");
        manual.Text="12000"; ApplyManual(); Check(!engine.State.Automatic && engine.State.Weight==12000,"Manual mode did not recover.");
        minimum.Value=30000; ApplyAuto(); Check(!engine.State.Automatic && mode.SelectedIndex==0,"Invalid automatic draft changed the mode.");
        minimum.Value=0; ApplyAuto(); engine.Pause(true); engine.SetInterval(20);
        Check(engine.State.Paused,"Timing change cleared pause.");
        Preset(20000); engine.Pause(false); engine.SetInterval(500); interval.Value=500;
        reduceMotion.Checked=true; UpdateView(); reduceMotion.Checked=false;
        Check(manualPanel.Visible && !autoPanel.Visible && connect.Enabled && !disconnect.Enabled,"Stopped controls are inconsistent.");
        // Retrying the same failure must report one real error in each new attempt.
        var occupied=new System.Net.Sockets.TcpListener(System.Net.IPAddress.Loopback,0); occupied.Start();
        try {
            port.Value=((System.Net.IPEndPoint)occupied.LocalEndpoint).Port;
            for(int attempt=0;attempt<2;attempt++) {
                await StartConnection();
                Check(active==null && activity.Snapshot.Errors==1,"Repeated occupied-port failure produced inaccurate error statistics.");
            }
        } finally { occupied.Stop(); }
        // Exercise actual Start/Stop buttons through the same handlers used by an operator.
        var probe=new System.Net.Sockets.TcpListener(System.Net.IPAddress.Loopback,0); probe.Start();
        int freePort=((System.Net.IPEndPoint)probe.LocalEndpoint).Port; probe.Stop();
        port.Value=freePort; interval.Value=20;
        await StartConnection();
        Check(active!=null && !connect.Enabled && disconnect.Enabled,"Start connection did not lock its settings.");
        using(var tcp=new System.Net.Sockets.TcpClient()) {
            await tcp.ConnectAsync(System.Net.IPAddress.Loopback,freePort);
            var bytes=new byte[14]; using var timeout=new CancellationTokenSource(6000);
            await tcp.GetStream().ReadExactlyAsync(bytes,timeout.Token);
            Check(bytes.SequenceEqual(FrameEncoder.Encode(20000)),"UI connection sent incorrect frame bytes.");
            dial.Value=12000;
            bool found=false;
            for(int i=0;i<8;i++) {
                await tcp.GetStream().ReadExactlyAsync(bytes,timeout.Token);
                if(bytes.SequenceEqual(FrameEncoder.Encode(12000))) { found=true; break; }
            }
            Check(found,"Live dial change did not reach TCP.");
            await Task.Delay(60); UpdateView();
            Check(activity.Snapshot.Writes>0 && scene.MotionRunning,"Real writes did not enable truck motion.");
            double moving=scene.MotionDistance; await Task.Delay(160);
            Check(scene.MotionDistance>moving,"Trucks did not move during data flow.");
            engine.Pause(true); UpdateView(); double stopped=scene.MotionDistance;
            await Task.Delay(160); Check(scene.MotionDistance==stopped,"Trucks moved while paused.");
            engine.Pause(false); Preset(0); UpdateView(); await Task.Delay(100); UpdateView();
            moving=scene.MotionDistance; await Task.Delay(160);
            Check(scene.MotionDistance>moving,"Zero-weight data incorrectly stopped truck motion.");
            reduceMotion.Checked=true; UpdateView(); stopped=scene.MotionDistance;
            await Task.Delay(160); Check(scene.MotionDistance==stopped,"Reduce motion did not freeze the truck lineup.");
            reduceMotion.Checked=false; Preset(20000); UpdateView();
            await Task.Delay(120); UpdateView(); SavePreview("Preview-active.png");
        }
        await StopConnection();
        Check(connect.Enabled && !disconnect.Enabled,"Stop connection did not release its controls.");
        double stoppedDistance=scene.MotionDistance; await Task.Delay(160);
        Check(scene.MotionDistance==stoppedDistance && !scene.MotionRunning,"Trucks moved after Stop connection.");
        Check(activity.Snapshot.Writes>0,"Stopping incorrectly cleared the real session statistics.");
        port.Value=10001; interval.Value=500; engine.SetInterval(500); Preset(20000);
        activity.Reset(); log.Items.Clear(); UpdateView();
        SetNotice("Ready. 1 Connect → 2 Choose a weight → 3 Stream.");
        layoutDirty=true; Reflow(); SavePreview("Preview.png");
        refresh.Start();
    }
    static IEnumerable<Control> Descendants(Control parent)
    {
        foreach(Control control in parent.Controls) { yield return control; foreach(var child in Descendants(control)) yield return child; }
    }
    void AssertLayout()
    {
        foreach(var control in Descendants(root).Where(c=>c.Visible))
        {
            var parent=control.Parent!;
            var area=parent.ClientRectangle;
            if(control.Left < -1 || control.Top < -1 || control.Right > area.Right+1 || control.Bottom > area.Bottom+1)
                throw new InvalidOperationException($"Clipped {control.GetType().Name} '{control.Text}' bounds={control.Bounds} parent={parent.GetType().Name} area={area}");
            if(control is WrappedLabel && control.GetPreferredSize(new Size(control.Width,0)).Height > control.Height+1)
                throw new InvalidOperationException($"Text clipped: {control.Text}");
            if(control is Button && control.GetPreferredSize(Size.Empty).Width>control.Width+1)
                throw new InvalidOperationException($"Button text clipped: {control.Text}");
        }
        if(workspace.Stacked && weightCard.Top<connectionCard.Bottom)
            throw new InvalidOperationException("Connection and weight sections overlap after stacking.");
        if(!workspace.Stacked && weightCard.Left<connectionCard.Right)
            throw new InvalidOperationException("Connection and weight sections overlap side by side.");
        if(scroll.HorizontalScroll.Visible) throw new InvalidOperationException("Unexpected horizontal scrollbar.");
    }
    async Task CheckResponsiveLayouts()
    {
        var original=ClientSize;
        var fonts=Descendants(root).ToDictionary(c=>c,c=>c.Font);
        var temporaryFonts=new List<Font>();
        try {
            foreach(float textScale in new[]{1f,1.5f,2f})
            {
                foreach(var pair in fonts) {
                    var f=new Font(pair.Value.FontFamily,pair.Value.Size*textScale,pair.Value.Style);
                    temporaryFonts.Add(f); pair.Key.Font=f;
                }
                foreach(int width in new[]{1280,640,800,1000,1280})
                {
                    ClientSize=new Size(width,800);
                    foreach(bool automatic in new[]{false,true})
                    {
                        if(automatic) ApplyAuto(); else Preset(20000);
                        foreach(int connection in new[]{0,1}) {
                            transport.SelectedIndex=connection;
                            showLog.Checked=width==800;
                            layoutDirty=true; Reflow(); await Task.Delay(20); layoutDirty=true; Reflow();
                            AssertLayout();
                            if(textScale==1 && connection==0 && ((width==1280&&!automatic)||(width==800&&automatic)))
                                SavePreview($"Preview-{width}-{(automatic?"automatic":"manual")}.png");
                        }
                    }
                }
            }
        }
        finally {
            foreach(var pair in fonts) pair.Key.Font=pair.Value;
            foreach(var font in temporaryFonts) font.Dispose();
            ClientSize=original; transport.SelectedIndex=0; showLog.Checked=false;
            Preset(20000); layoutDirty=true; Reflow(); scroll.AutoScrollPosition=Point.Empty;
        }
    }
    void SavePreview(string name)
    {
        layoutDirty=true; Reflow();
        using var image=new Bitmap(Width,Height);
        DrawToBitmap(image,new Rectangle(0,0,Width,Height));
        image.Save(Path.Combine(uiCheckFolder,name),System.Drawing.Imaging.ImageFormat.Png);
    }
    protected override void Dispose(bool disposing)
    {
        if(disposing) { refresh.Dispose(); help.Dispose(); }
        base.Dispose(disposing);
    }
}
