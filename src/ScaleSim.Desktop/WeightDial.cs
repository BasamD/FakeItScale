using System.Drawing.Drawing2D;
using System.ComponentModel;
namespace ScaleSim.Desktop;

public sealed class WeightDial : Control
{
    int value = 20000;
    public event EventHandler? ValueChanged;
    [DefaultValue(20000)]
    public int Value
    {
        get => value;
        set {
            if(value is < 0 or > 99999) throw new ArgumentOutOfRangeException(nameof(value));
            if(this.value == value) return;
            this.value = value; Invalidate(); ValueChanged?.Invoke(this, EventArgs.Empty);
        }
    }
    public WeightDial()
    {
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.Selectable, true);
        Size = new Size(224, 224); TabStop = true; Cursor = Cursors.Hand;
        AccessibleName = "Manual weight dial"; AccessibleDescription = "Drag to change pounds. Arrow keys change 20 pounds; Page Up and Page Down change 1000. Home is zero, End is 99999.";
    }
    void Adjust(int delta) => Value = Math.Clamp(Value + delta, 0, 99999);
    protected override bool IsInputKey(Keys keyData) => (keyData & Keys.KeyCode) is Keys.Left or Keys.Right or Keys.Up or Keys.Down or Keys.Home or Keys.End or Keys.PageUp or Keys.PageDown || base.IsInputKey(keyData);
    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        switch(e.KeyCode) {
            case Keys.Left: case Keys.Down: Adjust(-20); break;
            case Keys.Right: case Keys.Up: Adjust(20); break;
            case Keys.PageUp: Adjust(1000); break;
            case Keys.PageDown: Adjust(-1000); break;
            case Keys.Home: Value = 0; break;
            case Keys.End: Value = 99999; break;
            default: return;
        }
        e.Handled = true; e.SuppressKeyPress = true;
    }
    protected override void OnMouseDown(MouseEventArgs e) { base.OnMouseDown(e); if(e.Button == MouseButtons.Left) { Focus(); Capture = true; SetFromPoint(e.Location); } }
    protected override void OnMouseMove(MouseEventArgs e) { base.OnMouseMove(e); if(Capture && e.Button == MouseButtons.Left) SetFromPoint(e.Location); }
    protected override void OnMouseUp(MouseEventArgs e) { base.OnMouseUp(e); Capture = false; }
    protected override void OnMouseWheel(MouseEventArgs e) { base.OnMouseWheel(e); if(Focused) Adjust(Math.Sign(e.Delta)*20); }
    protected override void OnGotFocus(EventArgs e) { base.OnGotFocus(e); Invalidate(); }
    protected override void OnLostFocus(EventArgs e) { base.OnLostFocus(e); Invalidate(); }
    void SetFromPoint(Point p)
    {
        float dx=p.X-Width/2f, dy=p.Y-Height/2f;
        if(dx*dx+dy*dy < 100) return;
        double angle = (Math.Atan2(dy, dx)*180/Math.PI-135+360)%360;
        if(angle > 270) angle = angle < 315 ? 270 : 0;
        int target=(int)Math.Round(angle/270*99999);
        Value = target >= 99990 ? 99999 : Math.Clamp((int)Math.Round(target/20d)*20,0,99999);
    }
    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e); var g=e.Graphics; g.SmoothingMode=SmoothingMode.AntiAlias;
        float side=Math.Min(Width,Height), cx=Width/2f, cy=Height/2f, radius=side*.34f;
        var ring=new RectangleF(cx-radius,cy-radius,radius*2,radius*2);
        using var faint=new Pen(Theme.Border,10) { StartCap=LineCap.Round, EndCap=LineCap.Round };
        using var fill=new Pen(Enabled ? Theme.Accent : Theme.Muted,10) { StartCap=LineCap.Round, EndCap=LineCap.Round };
        g.DrawArc(faint,ring,135,270); if(value>0) g.DrawArc(fill,ring,135,value/99999f*270);
        using var ticks=new Pen(Theme.Muted,1);
        for(int i=0;i<=20;i++) {
            double a=(135+i*13.5)*Math.PI/180;
            float r1=radius+12,r2=radius+(i%5==0?21:16);
            g.DrawLine(ticks,cx+(float)Math.Cos(a)*r1,cy+(float)Math.Sin(a)*r1,cx+(float)Math.Cos(a)*r2,cy+(float)Math.Sin(a)*r2);
        }
        double needle=(135+value/99999d*270)*Math.PI/180;
        using var pointer=new SolidBrush(Enabled?Theme.Accent:Theme.Muted);
        g.FillEllipse(pointer,cx+(float)Math.Cos(needle)*radius-7,cy+(float)Math.Sin(needle)*radius-7,14,14);
        using var number=Theme.Font(21,true); using var unit=Theme.Font(9); using var ink=new SolidBrush(Theme.Ink); using var gray=new SolidBrush(Theme.Muted);
        using var center=new StringFormat { Alignment=StringAlignment.Center,LineAlignment=StringAlignment.Center };
        g.DrawString(value.ToString("N0"),number,ink,new PointF(cx,cy-7),center);
        g.DrawString("POUNDS",unit,gray,new PointF(cx,cy+24),center);
        g.DrawString("0",unit,gray,new PointF(cx-radius*.85f,cy+radius*.95f),center);
        g.DrawString("99,999",unit,gray,new PointF(cx+radius*.8f,cy+radius*.95f),center);
        if(Focused) ControlPaint.DrawFocusRectangle(g,new Rectangle(3,3,Width-6,Height-6));
    }
}
