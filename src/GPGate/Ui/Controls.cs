using System.ComponentModel;

namespace GPGate;

// 只读多行文本框：原生背景和滚动条缓存在句柄里，主题变了要重建句柄
internal sealed class DetailsBox : TextBox
{
    private bool _dark = Application.IsDarkModeEnabled; // 与建句柄时的主题一致

    // 同色广播不重建，省得滚动位置被重置
    internal void ReloadTheme(bool dark)
    {
        if (_dark == dark)
            return;

        _dark = dark;
        if (IsHandleCreated)
            RecreateHandle();
    }
}

// 链接样式按钮：单行自绘 chevron + 文字，悬停下划线；用按钮类避免静态文本的双击取词/复制
internal sealed class LinkButton : Button
{
    private const int IconGap = 4;

    private readonly Font _iconFont = new("Segoe Fluent Icons", 9F);
    private string _icon = "";
    private bool _hover;

    internal LinkButton()
    {
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint
            | ControlStyles.OptimizedDoubleBuffer, true);
        SetStyle(ControlStyles.Selectable, false); // 链接不参与 Tab/焦点
        Cursor = Cursors.Hand;
        AutoSize = true;
    }

    // .NET 10 对 Button 默认隐式应用主题绘制，会绕过 OnPaint；完全自绘需要 opt-out
    protected override CreateParams CreateParams
    {
        get
        {
            SetStyle(ControlStyles.ApplyThemingImplicitly, false);
            return base.CreateParams;
        }
    }

    [DefaultValue("")]
    internal string Icon
    {
        get => _icon;
        set
        {
            _icon = value;
            PerformLayout();
            Invalidate();
        }
    }

    // 图标占位按 4px 取整，免得展开/收起的箭头差 1px 让文字左右跳；调用方据此对齐内容列
    internal int IconOffset => (Measure(_icon, _iconFont).Width + 3) / 4 * 4 + IconGap;

    public override Size GetPreferredSize(Size proposedSize)
    {
        Size icon = Measure(_icon, _iconFont);
        Size text = Measure(Text, Font);
        return new Size(IconOffset + text.Width, Math.Max(icon.Height, text.Height));
    }

    protected override void OnMouseEnter(EventArgs e)
    {
        _hover = true;
        Invalidate();
        base.OnMouseEnter(e);
    }

    protected override void OnMouseLeave(EventArgs e)
    {
        _hover = false;
        Invalidate();
        base.OnMouseLeave(e);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        Graphics g = e.Graphics;
        g.Clear(Parent?.BackColor ?? SystemColors.Control);

        Size iconSize = Measure(_icon, _iconFont);
        Size textSize = Measure(Text, Font);

        TextRenderer.DrawText(g, _icon, _iconFont, new Rectangle(0, 0, iconSize.Width, Height),
            ForeColor, TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);

        Rectangle textRect = new(IconOffset, 0, textSize.Width, Height);
        TextRenderer.DrawText(g, Text, Font, textRect, ForeColor,
            TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);

        if (_hover)
        {
            int underlineY = Math.Min((Height + textSize.Height) / 2, Height - 1);
            using Pen pen = new(ForeColor);
            g.DrawLine(pen, textRect.Left, underlineY, textRect.Right, underlineY);
        }
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing); // 先销毁控件再释放字体

        if (disposing)
            _iconFont.Dispose();
    }

    private static Size Measure(string text, Font font) =>
        TextRenderer.MeasureText(text, font, Size.Empty,
            TextFormatFlags.NoPadding | TextFormatFlags.SingleLine);
}
