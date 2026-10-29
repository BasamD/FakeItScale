using System.Drawing.Drawing2D;
namespace ScaleSim.Desktop;

public sealed class TruckScene : Control
{
    float position = -180;
    long lastWrites;
    float pulse;
    bool initialized;
    public TruckScene() { DoubleBuffered = true; Height = 108; BackColor = Color.White; AccessibleName = "Truck on scale illustration"; }
    public void Advance(int sentWeight, long writes, bool connected, bool paused, bool reduceMotion)
    {
        if(!initialized) { position = -180; initialized = true; }
        if(writes != lastWrites) { pulse=1; lastWrites=writes; }
        pulse=Math.Max(0,pulse-.12f);
        float target=sentWeight>0 ? Math.Max(0,(Width-180)/2f) : Width+15;
        if(reduceMotion) { position=connected && writes>0 ? target : -180; pulse=0; }
        else if(connected && !paused && writes>0) {
            if(sentWeight>0 && position > Width) position=-180;
            position += Math.Clamp(target-position,-14,14);
        }
        Invalidate();
    }
    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e); var g=e.Graphics; g.SmoothingMode=SmoothingMode.AntiAlias;
        float y=Height-24;
        using var road=new Pen(Theme.Border,2); g.DrawLine(road,0,y+8,Width,y+8);
        using var platform=new SolidBrush(Color.FromArgb(216,235,235));
        g.FillRectangle(platform,Width*.25f,y-2,Width*.5f,12);
        using var outline=new Pen(Theme.Accent,2); g.DrawLine(outline,Width*.25f,y-2,Width*.75f,y-2);
        using var ink=new SolidBrush(Theme.Ink); using var body=new SolidBrush(Theme.Accent); using var window=new SolidBrush(Color.FromArgb(202,235,244));
        float x=position, top=y-53;
        g.FillRectangle(body,x,top,114,37); g.FillRectangle(ink,x-2,top+37,170,6);
        using var cab=new GraphicsPath(); cab.AddPolygon(new[]{new PointF(x+121,top+7),new PointF(x+148,top+7),new PointF(x+169,top+29),new PointF(x+169,top+40),new PointF(x+121,top+40)}); g.FillPath(body,cab);
        g.FillRectangle(window,x+127,top+12,17,15);
        using var tire=new SolidBrush(Color.FromArgb(43,57,72)); using var hub=new SolidBrush(Color.White);
        foreach(float wheel in new[]{18f,88f,145f}) { g.FillEllipse(tire,x+wheel-10,y-20,20,20); g.FillEllipse(hub,x+wheel-4,y-14,8,8); }
        if(pulse>0) { using var activity=new SolidBrush(Color.FromArgb((int)(pulse*150),Theme.Accent)); g.FillEllipse(activity,Width*.75f+12,y-7,10,10); }
    }
}
