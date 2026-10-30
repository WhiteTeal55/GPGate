namespace GPGate;

// 消息对话框：标准控件 + 系统配色
internal sealed class DialogForm : Form
{
    private const int DialogWidth = 520;
    private const int ContentLeft = 76;
    private const int ContentWidth = DialogWidth - ContentLeft - 24;
    private const int DetailsHeight = 110;

    private readonly Bitmap? _icon;
    private readonly Icon _windowIcon;
    private readonly Font _headingFont = new("Segoe UI Semibold", 10F);

    internal DialogForm(TaskDialogIcon icon, string text, string? heading, string? details)
    {
        Application.SetColorMode(SystemColorMode.System); // 每次打开都按系统深浅色重设

        _windowIcon = AppInfo.LoadAppIcon();

        Text = AppInfo.Name;
        Icon = _windowIcon;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        ShowInTaskbar = false;
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
        TextBox? detailsBox = null;
        if (details is not null)
        {
            // 链接样式展开器：自绘成按钮，chevron 与文字同行，也不会被双击取词
            link = new LinkButton
            {
                Text = "显示详细信息",
                Icon = "\uE76C", // ChevronRight
                ForeColor = (Application.IsDarkModeEnabled ? Palette.Dark : Palette.Light).ChipText,
            };
            // 文字对齐内容列，chevron 溢出到左侧
            link.Location = new Point(ContentLeft - link.GetIconOffset(), y + 10);
            Controls.Add(link);

            detailsBox = new TextBox
            {
                Multiline = true,
                ReadOnly = true,
                ScrollBars = ScrollBars.Vertical,
                BorderStyle = BorderStyle.FixedSingle,
                Bounds = new Rectangle(ContentLeft, link.Bottom + 3, ContentWidth, DetailsHeight),
                Visible = false,
                Text = details,
            };
            Controls.Add(detailsBox);
        }

        Panel separator = new()
        {
            Bounds = new Rectangle(0, (link?.Bottom ?? y) + 20, DialogWidth, 1),
            BackColor = SystemColors.ControlDark,
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
        int separatorTop = separator.Top;
        ClientSize = new Size(DialogWidth, ok.Bottom + 12);

        Shown += (_, _) => ActiveControl = ok; // 默认焦点给确定

        bool expanded = false;
        if (link is not null)
            link.Click += (_, _) => Toggle();

        // 原生 TaskDialog 展开/收起是瞬间换高
        void Toggle()
        {
            expanded = !expanded;
            link!.Icon = expanded ? "\uE70D" : "\uE76C"; // ChevronDown / ChevronRight
            link.Text = expanded ? "隐藏详细信息" : "显示详细信息";
            link.Left = ContentLeft - link.GetIconOffset();
            detailsBox!.Visible = expanded;
            separator.Top = separatorTop + (expanded ? DetailsHeight + 12 : 0);
            ok.Top = separator.Bottom + 12;
            ClientSize = new Size(DialogWidth, ok.Bottom + 12);
        }
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
