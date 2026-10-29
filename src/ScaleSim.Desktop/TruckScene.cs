using System.Diagnostics;
using System.Drawing.Drawing2D;
namespace ScaleSim.Desktop;

// This control only illustrates feed activity. It never reads or changes a weight,
// increments a transmission counter, or influences the transport clock.
public sealed class TruckScene : Control
{
    const double Speed = 50; // Logical drawing units per second, independent of display scaling.
    const float Spacing = 294;
    const int Variants = 3;
    readonly System.Windows.Forms.Timer animationTimer = new() { Interval = 33 };
    long lastTick;
    double distance;
    bool flowActive, reduceMotion;

    internal double MotionDistance => distance;
    internal bool MotionRunning => animationTimer.Enabled;

    public TruckScene()
    {
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
            ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
        Height = 112;
        BackColor = Color.White;
        TabStop = false;
        AccessibleName = "Scale feed activity illustration";
        AccessibleDescription = "A line of trucks moves while data is flowing. The illustration does not represent weights or confirm WasteWORKS capture.";
        animationTimer.Tick += Animate;
    }

    // Called on the UI thread using the monitor's actual-write freshness state.
    public void SetFlow(bool active, bool reduce)
    {
        bool changed = flowActive != active || reduceMotion != reduce;
        flowActive = active;
        reduceMotion = reduce;
        UpdateTimer();
        if(changed) Invalidate();
    }

    void UpdateTimer()
    {
        if(IsDisposed || Disposing) return;
        bool run = IsHandleCreated && Visible && flowActive && !reduceMotion;
        if(run && !animationTimer.Enabled)
        {
            // Resume from this instant, rather than catching up for time spent paused/hidden.
            lastTick = Stopwatch.GetTimestamp();
            animationTimer.Start();
        }
        else if(!run) animationTimer.Stop();
    }

    void Animate(object? sender, EventArgs e)
    {
        if(!flowActive || reduceMotion || !Visible) { UpdateTimer(); return; }
        long now = Stopwatch.GetTimestamp();
        double elapsed = Stopwatch.GetElapsedTime(lastTick, now).TotalSeconds;
        lastTick = now;
        // A blocked message loop must not make trucks jump a screen when it recovers.
        distance += Speed * Math.Clamp(elapsed, 0, .15);
        Invalidate();
    }

    protected override void OnHandleCreated(EventArgs e) { base.OnHandleCreated(e); UpdateTimer(); }
    protected override void OnHandleDestroyed(EventArgs e) { animationTimer.Stop(); base.OnHandleDestroyed(e); }
    protected override void OnVisibleChanged(EventArgs e) { base.OnVisibleChanged(e); UpdateTimer(); }
    protected override void Dispose(bool disposing)
    {
        if(disposing) animationTimer.Dispose();
        base.Dispose(disposing);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        if(Width < 8 || Height < 12) return;
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.PixelOffsetMode = PixelOffsetMode.HighQuality;
        float dpi = DeviceDpi / 96f;
        float scale = Math.Min(dpi * 1.12f, Math.Max(.1f, (Height - 14 * dpi) / 80f));
        float ground = Height - Math.Min(13 * dpi, Height * .16f);

        using var road = new Pen(Theme.Border, Math.Max(1, dpi));
        g.DrawLine(road, 0, ground, Width, ground);
        float deckLeft = Width * .25f, deckWidth = Width * .5f;
        using var deck = new SolidBrush(Color.FromArgb(225, 237, 239));
        g.FillRectangle(deck, deckLeft, ground, deckWidth, 7 * scale);
        using var deckEdge = new Pen(flowActive ? Theme.Accent : Theme.Muted, Math.Max(1, 2 * scale));
        g.DrawLine(deckEdge, deckLeft, ground, deckLeft + deckWidth, ground);
        using var support = new SolidBrush(Theme.Border);
        g.FillRectangle(support, deckLeft + 10 * scale, ground + 7 * scale, 8 * scale, 4 * scale);
        g.FillRectangle(support, deckLeft + deckWidth - 18 * scale, ground + 7 * scale, 8 * scale, 4 * scale);

        // The repeating pattern spans three whole truck spacings. Wrapping the phase
        // cannot swap the type or color of any visible truck at the scene boundary.
        double phase = distance % (Spacing * Variants);
        float logicalWidth = Width / scale;
        int first = (int)Math.Floor((-phase - 220) / Spacing);
        int last = (int)Math.Ceiling((logicalWidth - phase) / Spacing);
        for(int index = first; index <= last; index++)
        {
            float x = (float)(index * Spacing + phase);
            int variant = ((index % Variants) + Variants) % Variants;
            var saved = g.Save();
            g.TranslateTransform(x * scale, ground);
            g.ScaleTransform(scale, scale);
            DrawTruck(g, variant, distance);
            g.Restore(saved);
        }
    }

    static void DrawTruck(Graphics g, int variant, double travelled)
    {
        Color color = variant switch {
            0 => Theme.Accent,
            1 => Color.FromArgb(58, 92, 127),
            _ => Color.FromArgb(139, 107, 63)
        };
        using var body = new SolidBrush(color);
        using var frame = new SolidBrush(Theme.Ink);
        using var glass = new SolidBrush(Color.FromArgb(210, 237, 243));
        using var light = new SolidBrush(Color.FromArgb(248, 222, 155));
        using var shade = new Pen(Color.FromArgb(60, Color.White), 2);
        using var outline = new Pen(Color.FromArgb(90, Theme.Ink), 1);

        // Body choices: ribbed box, tipper, and tanker. All use the same wheelbase.
        if(variant == 0)
        {
            using var box = Theme.Round(new RectangleF(1, -72, 125, 46), 4);
            g.FillPath(body, box); g.DrawPath(outline, box);
            for(int x = 18; x < 124; x += 21) g.DrawLine(shade, x, -66, x, -31);
        }
        else if(variant == 1)
        {
            using var tipper = new GraphicsPath();
            tipper.AddPolygon(new[]{new PointF(0,-69),new PointF(128,-69),new PointF(117,-28),new PointF(14,-28)});
            g.FillPath(body, tipper); g.DrawPath(outline, tipper);
            for(int x = 24; x < 120; x += 25) g.DrawLine(shade, x, -62, x - 2, -33);
            g.DrawLine(shade, 6, -65, 123, -65);
        }
        else
        {
            using var tank = Theme.Round(new RectangleF(1, -70, 126, 42), 18);
            g.FillPath(body, tank); g.DrawPath(outline, tank);
            for(int x = 30; x < 120; x += 35) g.DrawLine(shade, x, -67, x, -31);
            g.FillRectangle(frame, 44, -74, 15, 4);
            g.FillRectangle(frame, 85, -74, 15, 4);
        }

        g.FillRectangle(frame, -1, -27, 200, 8);
        using var cab = new GraphicsPath();
        cab.AddPolygon(new[]{new PointF(138,-64),new PointF(166,-64),new PointF(195,-36),new PointF(195,-20),new PointF(138,-20)});
        g.FillPath(body, cab); g.DrawPath(outline, cab);
        g.FillRectangle(glass, 145, -57, 17, 20);
        using var windshield = new GraphicsPath();
        windshield.AddPolygon(new[]{new PointF(167,-55),new PointF(183,-38),new PointF(167,-38)});
        g.FillPath(glass, windshield);
        g.FillRectangle(light, 188, -31, 7, 5);
        g.FillRectangle(frame, 194, -24, 8, 5);
        g.FillRectangle(frame, 146, -32, 8, 2);

        using var tire = new SolidBrush(Color.FromArgb(43, 57, 72));
        using var hub = new SolidBrush(Color.FromArgb(238, 243, 246));
        using var spoke = new Pen(Theme.Muted, 1.3f);
        foreach(float wheel in new[]{22f, 49f, 112f, 172f})
        {
            const float radius = 10;
            g.FillEllipse(tire, wheel-radius, -2*radius, 2*radius, 2*radius);
            g.FillEllipse(hub, wheel-4.8f, -radius-4.8f, 9.6f, 9.6f);
            double angle = travelled/radius;
            float dx = (float)Math.Cos(angle)*3.7f, dy = (float)Math.Sin(angle)*3.7f;
            g.DrawLine(spoke, wheel-dx, -radius-dy, wheel+dx, -radius+dy);
        }
    }
}
