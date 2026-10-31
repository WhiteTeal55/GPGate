using System.Drawing.Drawing2D;
using System.Media;

namespace GPGate;

internal static class Dialog
{
    internal static void Show(IWin32Window? owner, TaskDialogIcon icon, string text,
        string? heading = null, string? details = null)
    {
        using DialogForm dialog = new(icon, text, heading, details);
        // 没有 owner 时居中屏幕并给个任务栏按钮，有 owner 时居中于 owner
        dialog.StartPosition = owner is null ? FormStartPosition.CenterScreen : FormStartPosition.CenterParent;
        dialog.ShowInTaskbar = owner is null;
        dialog.Shown += (_, _) =>
        {
            dialog.Activate();
            WindowFocus.BringToForeground(dialog.Handle);
        };
        _ = owner is null ? dialog.ShowDialog() : dialog.ShowDialog(owner);
    }
}

internal sealed class DialogForm : Form
{
    private const int DialogWidth = 500;
    private const int ContentLeft = 88;
    private const int ContentRight = 24;
    private const int ContentWidth = DialogWidth - ContentLeft - ContentRight;
    private const int DetailsHeight = 110;

    private readonly Bitmap? _icon;
    private readonly Icon _windowIcon;
    private readonly Font _headingFont = new("Segoe UI Semibold", 10.5F);
    private readonly TextBox? _details;

    internal DialogForm(TaskDialogIcon icon, string text, string? heading, string? details)
    {
        SuspendLayout();
        ThemeRefresh.EnsureColorMode();

        Palette palette = Application.IsDarkModeEnabled ? Palette.Dark : Palette.Light;
        if (text.StartsWith("[错误]", StringComparison.Ordinal))
        {
            heading = "错误";
            text = text["[错误]".Length..].TrimStart();
        }

        _windowIcon = AppInfo.LoadAppIcon();

        Text = AppInfo.Name;
        Icon = _windowIcon;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        StartPosition = FormStartPosition.CenterParent;
        AutoScaleDimensions = new SizeF(96F, 96F);
        AutoScaleMode = AutoScaleMode.Dpi;

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
                Bounds = new Rectangle(24, 28, 48, 48),
            });
        }

        string title = heading ?? (icon == TaskDialogIcon.Error || icon == TaskDialogIcon.Warning
            ? "错误"
            : icon == TaskDialogIcon.Information ? "提示" : "");

        int contentTop = 24;
        if (title.Length > 0)
        {
            Controls.Add(new Label
            {
                Text = title,
                Font = _headingFont,
                ForeColor = palette.Text,
                Bounds = new Rectangle(ContentLeft, contentTop, ContentWidth, 24),
            });
            contentTop += 30;
        }

        Font messageFont = SystemFonts.MessageBoxFont ?? SystemFonts.DefaultFont;
        Size textSize = TextRenderer.MeasureText(text, messageFont, new Size(ContentWidth, int.MaxValue),
            TextFormatFlags.WordBreak | TextFormatFlags.TextBoxControl | TextFormatFlags.NoPadding);
        Label message = new()
        {
            Text = text,
            Font = messageFont,
            ForeColor = palette.Text,
            Bounds = new Rectangle(ContentLeft, contentTop, ContentWidth, Math.Max(textSize.Height, 22)),
        };
        Controls.Add(message);

        LinkButton? link = null;
        if (details is not null)
        {
            link = new LinkButton
            {
                Text = "显示详细信息",
                Icon = "\uE76C",
                ForeColor = palette.ChipText,
                Location = new Point(ContentLeft, message.Bottom + 8),
            };
            Controls.Add(link);

            _details = new DetailsBox
            {
                Multiline = true,
                ReadOnly = true,
                ScrollBars = ScrollBars.Vertical,
                BorderStyle = BorderStyle.FixedSingle,
                Bounds = new Rectangle(ContentLeft, link.Bottom + 8, ContentWidth, DetailsHeight),
                Visible = false,
                Text = details,
                BackColor = palette.FieldBackground,
                ForeColor = palette.Text,
            };
            Controls.Add(_details);
        }

        int contentBottom = link?.Bottom ?? message.Bottom;
        Button ok = new()
        {
            Text = "确定",
            DialogResult = DialogResult.OK,
            FlatStyle = FlatStyle.Flat,
            UseVisualStyleBackColor = false,
            BackColor = Palette.Accent,
            ForeColor = Color.White,
            Bounds = new Rectangle(DialogWidth - ContentRight - 88, contentBottom + 20, 88, 34),
        };
        ok.FlatAppearance.BorderSize = 0;
        ok.FlatAppearance.MouseOverBackColor = Color.FromArgb(0, 165, 240);
        ok.FlatAppearance.MouseDownBackColor = Color.FromArgb(0, 124, 188);
        ok.SizeChanged += (_, _) => ApplyRoundedCorners(ok);
        Controls.Add(ok);
        ApplyRoundedCorners(ok);

        AcceptButton = ok;
        CancelButton = ok;
        ClientSize = new Size(DialogWidth, ok.Bottom + 20);
        ResumeLayout(false);
        PerformLayout();

        SystemSound sound = icon == TaskDialogIcon.Error ? SystemSounds.Hand
            : icon == TaskDialogIcon.Warning ? SystemSounds.Exclamation
            : icon == TaskDialogIcon.Information ? SystemSounds.Asterisk
            : SystemSounds.Beep;
        Shown += (_, _) =>
        {
            ActiveControl = ok;
            WindowAttention.Notify(this, sound);
        };

        bool expanded = false;
        if (link is not null)
        {
            link.Click += (_, _) => Toggle();
            Load += (_, _) => AlignLink();
        }

        void AlignLink() =>
            link!.Left = LogicalToDeviceUnits(ContentLeft) - link.IconOffset;

        void Toggle()
        {
            expanded = !expanded;
            link!.Icon = expanded ? "\uE70D" : "\uE76C";
            link.Text = expanded ? "隐藏详细信息" : "显示详细信息";
            AlignLink();
            _details!.Visible = expanded;

            int gap = LogicalToDeviceUnits(20);
            ok.Top = link.Bottom + gap
                + (expanded ? _details.Height + LogicalToDeviceUnits(16) : 0);
            ClientSize = new Size(ClientSize.Width, ok.Bottom + gap);
        }
    }

    protected override void WndProc(ref Message m)
    {
        if (ThemeRefresh.TryApply(this, m))
            return;

        base.WndProc(ref m);
    }

    private static void ApplyRoundedCorners(Control control)
    {
        int diameter = Math.Min(16, Math.Min(control.Width, control.Height));
        int x = control.Width - diameter;
        int y = control.Height - diameter;

        using GraphicsPath path = new();
        path.AddArc(0, 0, diameter, diameter, 180, 90);
        path.AddArc(x, 0, diameter, diameter, 270, 90);
        path.AddArc(x, y, diameter, diameter, 0, 90);
        path.AddArc(0, y, diameter, diameter, 90, 90);
        path.CloseFigure();

        Region? previous = control.Region;
        control.Region = new Region(path);
        previous?.Dispose();
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);

        if (disposing)
        {
            _headingFont.Dispose();
            _icon?.Dispose();
            _windowIcon.Dispose();
        }
    }
}
