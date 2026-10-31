using System.Media;

namespace GPGate;

// 消息对话框：标准控件 + 系统配色
internal sealed class DialogForm : Form
{
    private const int DialogWidth = 520;
    private const int ContentLeft = 76;
    private const int ContentWidth = DialogWidth - ContentLeft - 24;
    private const int DetailsHeight = 110;

    // 补齐 Label 的内边距和字形留白，链接文字才和上方文字左对齐
    private const int LinkTextInset = 3;

    private readonly Bitmap? _icon;
    private readonly Icon _windowIcon;
    private readonly Font _headingFont = new("Segoe UI Semibold", 10F);
    private readonly TextBox? _details;

    internal DialogForm(TaskDialogIcon icon, string text, string? heading, string? details)
    {
        SuspendLayout();

        ThemeRefresh.EnsureColorMode();

        Palette palette = Application.IsDarkModeEnabled ? Palette.Dark : Palette.Light;

        _windowIcon = AppInfo.LoadAppIcon();

        Text = AppInfo.Name;
        Icon = _windowIcon;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        StartPosition = FormStartPosition.CenterParent;
        AutoScaleDimensions = new SizeF(96F, 96F);
        AutoScaleMode = AutoScaleMode.Dpi;

        int y = 22;
        if (icon != TaskDialogIcon.None)
        {
            _icon = ShellIcons.Load(icon == TaskDialogIcon.Error ? ShellIcons.Error
                : icon == TaskDialogIcon.Warning ? ShellIcons.Warning
                : icon == TaskDialogIcon.Information ? ShellIcons.Info
                : ShellIcons.Application);
            Controls.Add(new PictureBox
            {
                Image = _icon,
                SizeMode = PictureBoxSizeMode.CenterImage,
                Bounds = new Rectangle(12, y - 8, 48, 48),
            });
        }

        if (!string.IsNullOrWhiteSpace(heading))
        {
            Controls.Add(new Label
            {
                Text = heading,
                Font = _headingFont,
                Bounds = new Rectangle(ContentLeft, y, ContentWidth, 24),
            });
            y += 30;
        }

        Font messageFont = SystemFonts.MessageBoxFont ?? SystemFonts.DefaultFont;
        Size textSize = TextRenderer.MeasureText(text, messageFont, new Size(ContentWidth, int.MaxValue),
            TextFormatFlags.WordBreak | TextFormatFlags.TextBoxControl | TextFormatFlags.NoPadding);
        Controls.Add(new Label
        {
            Text = text,
            Font = messageFont,
            Bounds = new Rectangle(ContentLeft, y, ContentWidth, textSize.Height),
        });
        y += textSize.Height;

        LinkButton? link = null;
        if (details is not null)
        {
            // 链接样式展开器：自绘成按钮，chevron 与文字同行，也不会被双击取词
            link = new LinkButton
            {
                Text = "显示详细信息",
                Icon = "\uE76C", // ChevronRight
                ForeColor = palette.ChipText,
            };
            // 文字对齐内容列，chevron 溢到左侧；左缘在 Load 里按实际 DPI 校正
            link.Location = new Point(ContentLeft, y + 10);
            Controls.Add(link);

            // 外框由 OnPaint 画：文本框自带的边框不跟深浅色
            _details = new DetailsBox
            {
                Multiline = true,
                ReadOnly = true,
                ScrollBars = ScrollBars.Vertical,
                BorderStyle = BorderStyle.None,
                Bounds = new Rectangle(ContentLeft + 1, link.Bottom + 4, ContentWidth - 2, DetailsHeight - 2),
                Visible = false,
                Text = details,
                // 只读文本框默认是灰底
                BackColor = palette.FieldBackground,
                ForeColor = palette.Text,
            };
            Controls.Add(_details);
        }

        Panel separator = new()
        {
            Bounds = new Rectangle(0, (link?.Bottom ?? y) + 20, DialogWidth, 1),
            BackColor = palette.Separator,
        };
        Controls.Add(separator);

        Button ok = new()
        {
            Text = "确定",
            DialogResult = DialogResult.OK,
            Bounds = new Rectangle(DialogWidth - 24 - 88, separator.Bottom + 12, 88, 32),
        };
        Controls.Add(ok);

        AcceptButton = ok;
        CancelButton = ok;
        ClientSize = new Size(DialogWidth, ok.Bottom + 12);
        ResumeLayout(false);
        PerformLayout();

        SystemSound sound = icon == TaskDialogIcon.Error ? SystemSounds.Hand
            : icon == TaskDialogIcon.Warning ? SystemSounds.Exclamation
            : icon == TaskDialogIcon.Information ? SystemSounds.Asterisk
            : SystemSounds.Beep;
        Shown += (_, _) =>
        {
            ActiveControl = ok; // 默认焦点给确定
            WindowAttention.Notify(this, sound);
        };

        bool expanded = false;
        if (link is not null)
        {
            link.Click += (_, _) => Toggle();

            // chevron 宽是物理像素，AutoScale 完成后才对齐得上
            Load += (_, _) => AlignLink();
        }

        void AlignLink() => link!.Left = LogicalToDeviceUnits(ContentLeft) - link.IconOffset + LogicalToDeviceUnits(LinkTextInset);

        // 原生 TaskDialog 展开/收起是瞬间换高
        void Toggle()
        {
            expanded = !expanded;
            link!.Icon = expanded ? "\uE70D" : "\uE76C"; // ChevronDown / ChevronRight
            link.Text = expanded ? "隐藏详细信息" : "显示详细信息";
            AlignLink();
            _details!.Visible = expanded;
            Invalidate(Rectangle.Inflate(_details.Bounds, 1, 1)); // 外框跟着显示/擦掉

            int gap = LogicalToDeviceUnits(12);
            separator.Top = link.Bottom + LogicalToDeviceUnits(20)
                + (expanded ? _details.Height + 2 + gap : 0);
            ok.Top = separator.Bottom + gap;
            ClientSize = new Size(ClientSize.Width, ok.Bottom + gap);
        }
    }

    // 详细信息框的 1px 外框，颜色跟系统深浅色走
    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);

        if (_details is not { Visible: true } details)
            return;

        Rectangle frame = Rectangle.Inflate(details.Bounds, 1, 1);
        using Pen pen = new((Application.IsDarkModeEnabled ? Palette.Dark : Palette.Light).FieldBorder);
        e.Graphics.DrawRectangle(pen, frame.X, frame.Y, frame.Width - 1, frame.Height - 1);
    }

    protected override void WndProc(ref Message m)
    {
        if (ThemeRefresh.TryApply(this, m))
            return;

        base.WndProc(ref m);
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing); // 先销毁窗口再释放图标

        if (disposing)
        {
            _headingFont.Dispose();
            _icon?.Dispose();
            _windowIcon.Dispose();
        }
    }
}
