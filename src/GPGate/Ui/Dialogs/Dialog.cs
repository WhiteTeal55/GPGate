using System.Media;

namespace GPGate;

internal readonly record struct DialogOutcome(bool Confirmed, bool Checked);

internal static class Dialog
{
    internal static DialogOutcome Show(IWin32Window? owner, TaskDialogIcon icon, string text,
        string? heading = null, string? details = null, string? checkBoxText = null, bool showCancel = false)
    {
        using DialogForm dialog = new(icon, text, heading, details, checkBoxText, showCancel);
        // 无 owner 时居中屏幕并显示任务栏按钮
        dialog.StartPosition = owner is null ? FormStartPosition.CenterScreen : FormStartPosition.CenterParent;
        dialog.ShowInTaskbar = owner is null;
        dialog.Shown += (_, _) =>
        {
            dialog.Activate();
            WindowFocus.BringToForeground(dialog.Handle);
        };
        DialogResult result = owner is null ? dialog.ShowDialog() : dialog.ShowDialog(owner);
        return new(result == DialogResult.OK, dialog.Checked);
    }

    // 命令/日志结果转弹窗：去掉 [级别] 前缀，按级别选图标
    internal static void ShowResult(string message)
    {
        int end = message.IndexOf(']');
        if (end < 1 || message[0] != '[')
        {
            Show(null, TaskDialogIcon.Warning, message);
            return;
        }

        TaskDialogIcon icon = message.StartsWith("[错误]", StringComparison.Ordinal) ? TaskDialogIcon.Error
            : message.StartsWith("[信息]", StringComparison.Ordinal) ? TaskDialogIcon.Information
            : TaskDialogIcon.Warning;
        Show(null, icon, message[(end + 1)..].TrimStart());
    }
}

internal sealed class DialogForm : Form
{
    private const int DialogWidth = 520;
    private const int ContentLeft = 64;
    private const int ContentWidth = DialogWidth - ContentLeft - 24;
    private const int DetailsHeight = 110;

    private const int CheckBoxTop = 6;

    private readonly Bitmap? _icon;
    private readonly Icon _windowIcon;
    private readonly Font _headingFont = new(SystemFonts.MessageBoxFont?.FontFamily ?? SystemFonts.DefaultFont.FontFamily, 12F);
    private readonly TextBox? _details;
    private readonly CheckBox? _checkBox;

    internal DialogForm(TaskDialogIcon icon, string text, string? heading, string? details,
        string? checkBoxText, bool showCancel)
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

        int y = 20;
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
                Bounds = new Rectangle(20, y, 32, 32),
            });
        }

        if (!string.IsNullOrWhiteSpace(heading))
        {
            Controls.Add(new Label
            {
                Text = heading,
                Font = _headingFont,
                Bounds = new Rectangle(ContentLeft, y, ContentWidth, 26),
            });
            y += 32;
        }

        Font messageFont = SystemFonts.MessageBoxFont ?? SystemFonts.DefaultFont;
        if (!string.IsNullOrEmpty(text))
        {
            Size textSize = TextRenderer.MeasureText(text, messageFont, new Size(ContentWidth, int.MaxValue),
                TextFormatFlags.WordBreak | TextFormatFlags.TextBoxControl | TextFormatFlags.NoPadding);
            Controls.Add(new Label
            {
                Text = text,
                Font = messageFont,
                Bounds = new Rectangle(ContentLeft, y, ContentWidth, textSize.Height),
            });
            y += textSize.Height;
        }

        LinkLabel? link = null;
        if (details is not null)
        {
            link = new LinkLabel
            {
                Text = "显示详细信息",
                AutoSize = true,
                TabStop = true,
                LinkBehavior = LinkBehavior.HoverUnderline,
                LinkColor = palette.ChipText,
                ActiveLinkColor = palette.ChipText,
                VisitedLinkColor = palette.ChipText,
            };
            link.Location = new Point(ContentLeft, y + 10);
            Controls.Add(link);

            _details = new DetailsBox
            {
                Multiline = true,
                ReadOnly = true,
                ScrollBars = ScrollBars.Vertical,
                BorderStyle = BorderStyle.None,
                Bounds = new Rectangle(ContentLeft + 1, link.Bottom + 4, ContentWidth - 2, DetailsHeight - 2),
                Visible = false,
                Text = details,
                BackColor = palette.FieldBackground,
                ForeColor = palette.Text,
            };
            Controls.Add(_details);
        }

        int contentBottom = link?.Bottom ?? y;

        Panel separator = new()
        {
            Bounds = new Rectangle(0, contentBottom + 20, DialogWidth, 1),
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

        Button? cancel = null;
        if (showCancel)
        {
            cancel = new Button
            {
                Text = "取消",
                DialogResult = DialogResult.Cancel,
                Bounds = new Rectangle(ok.Left - 12 - 88, ok.Top, 88, 32),
            };
            Controls.Add(cancel);
        }

        if (checkBoxText is not null)
        {
            _checkBox = new CheckBox
            {
                Text = checkBoxText,
                Font = messageFont,
                AutoSize = true,
                Location = new Point(24, ok.Top + CheckBoxTop),
            };
            Controls.Add(_checkBox);
        }

        AcceptButton = ok;
        CancelButton = cancel ?? ok;
        ClientSize = new Size(DialogWidth, ok.Bottom + 12);
        ResumeLayout(false);
        PerformLayout();

        Shown += (_, _) =>
        {
            ActiveControl = ok;
            PlaySystemSound(icon);
        };

        bool expanded = false;
        if (link is not null)
            link.LinkClicked += (_, _) => Toggle();

        // 原生 TaskDialog 瞬间换高
        void Toggle()
        {
            expanded = !expanded;
            link!.Text = expanded ? "隐藏详细信息" : "显示详细信息";
            _details!.Visible = expanded;
            Invalidate(Rectangle.Inflate(_details.Bounds, 1, 1));

            int gap = LogicalToDeviceUnits(12);
            separator.Top = (expanded ? _details.Bottom : link!.Bottom) + LogicalToDeviceUnits(20);
            ok.Top = separator.Bottom + gap;
            if (cancel is not null)
                cancel.Top = ok.Top;
            if (_checkBox is not null)
                _checkBox.Top = ok.Top + LogicalToDeviceUnits(CheckBoxTop);

            ClientSize = new Size(ClientSize.Width, ok.Bottom + gap);

            // 改文本后 LinkLabel 会重置光标，鼠标未移动时手动恢复
            link.Cursor = link.ClientRectangle.Contains(link.PointToClient(Cursor.Position))
                ? Cursors.Hand
                : Cursors.Default;
        }
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);

        if (_details is not { Visible: true } details)
            return;

        Rectangle frame = Rectangle.Inflate(details.Bounds, 1, 1);
        using Pen pen = new((Application.IsDarkModeEnabled ? Palette.Dark : Palette.Light).FieldBorder);
        e.Graphics.DrawRectangle(pen, frame.X, frame.Y, frame.Width - 1, frame.Height - 1);
    }

    internal bool Checked => _checkBox?.Checked ?? false;

    // 跟随系统声音方案；无图标不发声
    private static void PlaySystemSound(TaskDialogIcon icon)
    {
        SystemSound? sound = icon == TaskDialogIcon.Error ? SystemSounds.Hand
            : icon == TaskDialogIcon.Warning ? SystemSounds.Exclamation
            : icon == TaskDialogIcon.Information ? SystemSounds.Asterisk
            : null;

        sound?.Play();
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
