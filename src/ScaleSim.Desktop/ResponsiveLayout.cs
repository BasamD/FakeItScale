namespace ScaleSim.Desktop;

// These containers measure content at the available width before arranging it.
// No fixed section heights: a wrapped button row makes its section taller.
internal static class LayoutSize
{
    public static int Px(Control control, int value) => (int)Math.Ceiling(value * control.DeviceDpi / 96d);
    public static Size Measure(Control control, int width)
    {
        width = Math.Max(1, width);
        var result = control.GetPreferredSize(new Size(width, 0));
        return new Size(Math.Min(width, Math.Max(1, result.Width)), Math.Max(1, result.Height));
    }
}

internal class StackLayout : Panel
{
    [System.ComponentModel.DefaultValue(6)]
    public int Spacing { get; set; } = 6;
    public StackLayout(params Control[] controls)
    {
        Margin = Padding.Empty;
        Controls.AddRange(controls);
    }
    public override Size GetPreferredSize(Size proposedSize)
    {
        int width = proposedSize.Width > 0 ? proposedSize.Width : Math.Max(1, Width);
        int inner = Math.Max(1, width-Padding.Horizontal), height = Padding.Vertical;
        bool first = true;
        foreach(Control c in Controls)
        {
            if(!c.Visible) continue;
            if(!first) height += LayoutSize.Px(this, Spacing);
            height += LayoutSize.Measure(c, inner-c.Margin.Horizontal).Height + c.Margin.Vertical;
            first = false;
        }
        return new Size(width,height);
    }
    protected override void OnLayout(LayoutEventArgs e)
    {
        base.OnLayout(e);
        int y=Padding.Top, width=Math.Max(1,ClientSize.Width-Padding.Horizontal);
        foreach(Control c in Controls)
        {
            if(!c.Visible) continue;
            var size=LayoutSize.Measure(c,width-c.Margin.Horizontal);
            c.SetBounds(Padding.Left+c.Margin.Left,y+c.Margin.Top,Math.Max(1,width-c.Margin.Horizontal),size.Height);
            c.PerformLayout(); y+=size.Height+c.Margin.Vertical+LayoutSize.Px(this,Spacing);
        }
    }
}
internal sealed class WrapLayout : Panel
{
    public WrapLayout(params Control[] controls) { Margin=Padding.Empty; Controls.AddRange(controls); }
    Size Arrange(int width,bool apply)
    {
        int x=0,y=0,lineHeight=0,maxWidth=0,gap=LayoutSize.Px(this,8);
        foreach(Control c in Controls)
        {
            if(!c.Visible) continue;
            var size=LayoutSize.Measure(c,width-c.Margin.Horizontal);
            int outerWidth=size.Width+c.Margin.Horizontal, outerHeight=size.Height+c.Margin.Vertical;
            if(x>0 && x+outerWidth>width) { y+=lineHeight+gap; x=0; lineHeight=0; }
            if(apply) { c.SetBounds(x+c.Margin.Left,y+c.Margin.Top,size.Width,size.Height); c.PerformLayout(); }
            lineHeight=Math.Max(lineHeight,outerHeight); maxWidth=Math.Max(maxWidth,x+outerWidth); x+=outerWidth+gap;
        }
        return new Size(maxWidth,y+lineHeight);
    }
    public override Size GetPreferredSize(Size proposedSize) => Arrange(Math.Max(1,proposedSize.Width>0?proposedSize.Width:Width),false);
    protected override void OnLayout(LayoutEventArgs e) { base.OnLayout(e); Arrange(Math.Max(1,ClientSize.Width),true); }
}
internal sealed class FieldLayout : StackLayout
{
    readonly int desiredWidth;
    public FieldLayout(string title,Control input,int width=144) : base(Theme.Label(title,9,color:Theme.Muted),input)
    { desiredWidth=width; Spacing=3; }
    public override Size GetPreferredSize(Size proposedSize) => base.GetPreferredSize(new Size(Math.Min(proposedSize.Width>0?proposedSize.Width:int.MaxValue,LayoutSize.Px(this,desiredWidth)),0));
}
internal sealed class ResponsiveColumns : Panel
{
    readonly Control left,right;
    readonly int leftWidth,rightMinimum;
    internal bool Stacked { get; private set; }
    public ResponsiveColumns(Control left,Control right,int leftWidth,int rightMinimum)
    {
        this.left=left; this.right=right; this.leftWidth=leftWidth; this.rightMinimum=rightMinimum;
        Margin=Padding.Empty; Controls.Add(left); Controls.Add(right);
    }
    Size Arrange(int width,bool apply)
    {
        int gap=LayoutSize.Px(this,14), preferredLeft=LayoutSize.Px(this,leftWidth);
        bool stack=width<preferredLeft+LayoutSize.Px(this,rightMinimum)+gap;
        int lw=stack?width:preferredLeft, rw=stack?width:width-lw-gap;
        var ls=LayoutSize.Measure(left,lw); var rs=LayoutSize.Measure(right,rw);
        if(apply) {
            Stacked=stack;
            left.SetBounds(0,0,lw,ls.Height);
            right.SetBounds(stack?0:lw+gap,stack?ls.Height+gap:0,rw,rs.Height);
            left.PerformLayout(); right.PerformLayout();
        }
        return new Size(width,stack?ls.Height+gap+rs.Height:Math.Max(ls.Height,rs.Height));
    }
    public override Size GetPreferredSize(Size proposedSize) => Arrange(Math.Max(1,proposedSize.Width>0?proposedSize.Width:Width),false);
    protected override void OnLayout(LayoutEventArgs e) { base.OnLayout(e); Arrange(Math.Max(1,ClientSize.Width),true); }
}
internal sealed class WrappedLabel : Label
{
    public WrappedLabel() { AutoSize=false; UseMnemonic=false; Margin=Padding.Empty; }
    public override Size GetPreferredSize(Size proposedSize)
    {
        int width=proposedSize.Width>0?proposedSize.Width:10000;
        var size=TextRenderer.MeasureText(Text.Length>0?Text:" ",Font,new Size(Math.Max(1,width-Padding.Horizontal),int.MaxValue),
            TextFormatFlags.WordBreak|TextFormatFlags.TextBoxControl|TextFormatFlags.NoPrefix);
        return new Size(Math.Min(width,size.Width+Padding.Horizontal),size.Height+Padding.Vertical+2);
    }
}
