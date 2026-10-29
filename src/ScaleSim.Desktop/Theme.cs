using System.Drawing.Drawing2D;
namespace ScaleSim.Desktop;

internal static class Theme
{
    public static readonly Color Background = Color.FromArgb(242, 245, 249);
    public static readonly Color Ink = Color.FromArgb(24, 43, 64);
    public static readonly Color Muted = Color.FromArgb(87, 106, 124);
    public static readonly Color Accent = Color.FromArgb(0, 112, 122);
    public static readonly Color Border = Color.FromArgb(215, 224, 232);
    public static Font Font(float size = 10, bool bold = false) => new("Segoe UI", size, bold ? FontStyle.Bold : FontStyle.Regular);
    public static Label Label(string text, float size = 10, bool bold = false, Color? color = null) =>
        new WrappedLabel() { Text = text, ForeColor = color ?? Ink, Font = Font(size, bold), Margin = new Padding(0, 0, 6, 6) };
    public static GraphicsPath Round(RectangleF rect, float radius)
    {
        var path = new GraphicsPath(); float d = radius * 2;
        path.AddArc(rect.X, rect.Y, d, d, 180, 90); path.AddArc(rect.Right-d, rect.Y, d, d, 270, 90);
        path.AddArc(rect.Right-d, rect.Bottom-d, d, d, 0, 90); path.AddArc(rect.X, rect.Bottom-d, d, d, 90, 90);
        path.CloseFigure(); return path;
    }
}
internal sealed class Card : StackLayout
{
    public Card() { DoubleBuffered = true; BackColor = Color.White; Padding = new Padding(16); Margin = Padding.Empty; }
    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e); e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        using var pen = new Pen(Theme.Border);
        using var path = Theme.Round(new RectangleF(0, 0, Width-1, Height-1), 9);
        e.Graphics.DrawPath(pen, path);
    }
}
