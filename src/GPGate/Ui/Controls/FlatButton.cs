using System.Drawing.Drawing2D;

namespace GPGate;

internal enum GlyphKind
{
    None,
    Minimize,
    Close,
    Sun,
    Moon,
}

// 自绘圆角按钮：保留 Button 的点击/键盘行为，只换外观。
internal sealed class FlatButton : Button
{
    private bool _hover;
    private bool _pressed;

    internal Color Fill = Color.Empty;
    internal Color FillHover = Color.Empty;
    internal Color FillPress = Color.Empty;
    internal Color ForeColorHover = Color.Empty;
    internal GlyphKind Glyph = GlyphKind.None;
    internal int Radius = 8;
    internal float DpiScale = 1f;

    public FlatButton(string text)
    {
        Text = text;
        FlatStyle = FlatStyle.Flat;
        FlatAppearance.BorderSize = 0;
        UseVisualStyleBackColor = false;
        Cursor = Cursors.Hand;
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
                 ControlStyles.OptimizedDoubleBuffer | ControlStyles.SupportsTransparentBackColor, true);
    }

    // 未指定填充色时用控件背景，等同透明
    private Color CurrentFill
    {
        get
        {
            Color fill = _pressed ? FillPress : _hover ? FillHover : Fill;
            return fill.IsEmpty ? BackColor : fill;
        }
    }

    private Color CurrentText =>
        (_hover || _pressed) && ForeColorHover != Color.Empty ? ForeColorHover : ForeColor;

    protected override void OnMouseEnter(EventArgs e)
    {
        _hover = true;
        Invalidate();
        base.OnMouseEnter(e);
    }

    protected override void OnMouseLeave(EventArgs e)
    {
        _hover = false;
        _pressed = false;
        Invalidate();
        base.OnMouseLeave(e);
    }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        _pressed = true;
        Invalidate();
        base.OnMouseDown(e);
    }

    protected override void OnMouseUp(MouseEventArgs e)
    {
        _pressed = false;
        Invalidate();
        base.OnMouseUp(e);
    }

    protected override void OnGotFocus(EventArgs e)
    {
        Invalidate();
        base.OnGotFocus(e);
    }

    protected override void OnLostFocus(EventArgs e)
    {
        Invalidate();
        base.OnLostFocus(e);
    }

    protected override void OnEnabledChanged(EventArgs e)
    {
        _hover = _pressed = false;
        Invalidate();
        base.OnEnabledChanged(e);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        Graphics g = e.Graphics;

        // 铺底不能抗锯齿，否则边缘会和黑色缓冲混出色框
        g.SmoothingMode = SmoothingMode.None;
        using (SolidBrush back = new(BackColor))
            g.FillRectangle(back, ClientRectangle);

        g.SmoothingMode = SmoothingMode.AntiAlias;

        // 内缩 1px，避免抗锯齿边缘被控件边界裁掉
        Rectangle bounds = new(1, 1, Width - 2, Height - 2);
        int radius = Math.Min(Radius, Math.Min(bounds.Width, bounds.Height) / 2);
        using GraphicsPath path = Shapes.RoundRect(bounds, radius);
        Color fill = Dimmed(CurrentFill);
        using (SolidBrush brush = new(fill))
        using (Pen pen = new(fill))
        {
            g.FillPath(brush, path);
            g.DrawPath(pen, path); // 补同色描边，消掉边缘暗边
        }

        // 键盘操作时才显示焦点框
        if (Focused && ShowFocusCues)
            DrawFocusRing(g, bounds, radius);

        Color text = Dimmed(CurrentText);
        if (Glyph == GlyphKind.None)
            TextRenderer.DrawText(g, Text, Font, ClientRectangle, text, TextFlags | TextFormatFlags.NoPadding);
        else
            DrawGlyph(g, text);
    }

    private TextFormatFlags TextFlags =>
        (TextAlign == ContentAlignment.MiddleRight ? TextFormatFlags.Right : TextFormatFlags.HorizontalCenter)
        | TextFormatFlags.VerticalCenter;

    // 禁用时颜色向背景靠拢一半
    private Color Dimmed(Color color) => Enabled ? color : Color.FromArgb(
        (color.R + BackColor.R) / 2, (color.G + BackColor.G) / 2, (color.B + BackColor.B) / 2);

    // 画线代替字形，才能像素级居中
    private void DrawGlyph(Graphics g, Color color)
    {
        using Pen pen = new(color, 1.6f * DpiScale) { StartCap = LineCap.Round, EndCap = LineCap.Round };
        float cx = Width / 2f;
        float cy = Height / 2f;
        float half = 5f * DpiScale;
        float arm = 4f * DpiScale;

        if (Glyph == GlyphKind.Sun)
        {
            float radius = 3f * DpiScale;
            g.DrawEllipse(pen, cx - radius, cy - radius, radius * 2, radius * 2);
            for (int i = 0; i < 8; i++)
            {
                double angle = i * Math.PI / 4;
                float inner = 5f * DpiScale;
                float outer = 7f * DpiScale;
                g.DrawLine(pen,
                    cx + (float)Math.Cos(angle) * inner,
                    cy + (float)Math.Sin(angle) * inner,
                    cx + (float)Math.Cos(angle) * outer,
                    cy + (float)Math.Sin(angle) * outer);
            }
            return;
        }

        if (Glyph == GlyphKind.Moon)
        {
            float radius = 6f * DpiScale;
            Color fill = Dimmed(CurrentFill);
            using SolidBrush moon = new(color);
            using SolidBrush cutout = new(fill);
            g.FillEllipse(moon, cx - radius, cy - radius, radius * 2, radius * 2);
            g.FillEllipse(cutout, cx - radius + 4f * DpiScale, cy - radius - 2f * DpiScale,
                radius * 2, radius * 2);
            return;
        }

        if (Glyph == GlyphKind.Minimize)
        {
            g.DrawLine(pen, cx - half, cy, cx + half, cy);
            return;
        }

        g.DrawLine(pen, cx - arm, cy - arm, cx + arm, cy + arm);
        g.DrawLine(pen, cx + arm, cy - arm, cx - arm, cy + arm);
    }

    private void DrawFocusRing(Graphics g, Rectangle bounds, int radius)
    {
        int inset = (int)Math.Round(3 * DpiScale);
        Rectangle ring = Rectangle.Inflate(bounds, -inset, -inset);
        using Pen pen = new(CurrentText, Math.Max(1f, 1.2f * DpiScale)) { DashStyle = DashStyle.Dot };
        using GraphicsPath path = Shapes.RoundRect(ring, Math.Max(radius - inset, 2));
        g.DrawPath(pen, path);
    }
}
