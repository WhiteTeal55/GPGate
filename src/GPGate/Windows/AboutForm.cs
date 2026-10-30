using System.Diagnostics;

namespace GPGate;

// 关于窗口：标准控件 + 系统配色
internal sealed class AboutForm : Form
{
    private const int MarginX = 20;
    private const int TextX = 80;
    private const int TextWidth = 360;

    private readonly Icon _icon;
    private readonly Bitmap _logo;
    private readonly Font _titleFont;

    internal AboutForm()
    {
        // 每次打开都按系统深浅色重设
        Application.SetColorMode(SystemColorMode.System);

        _icon = AppInfo.LoadAppIcon();
        _logo = _icon.ToBitmap();
        _titleFont = new Font("Segoe UI Semibold", 15F);

        SuspendLayout();

        Text = "关于 " + AppInfo.Name;
        Icon = _icon;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        ShowInTaskbar = true;
        StartPosition = FormStartPosition.CenterScreen;
        AutoScaleDimensions = new SizeF(96F, 96F);
        AutoScaleMode = AutoScaleMode.Dpi;
        ClientSize = new Size(400, 234);

        PictureBox logo = new()
        {
            Bounds = new Rectangle(MarginX, 18, 44, 44),
            Image = _logo,
            SizeMode = PictureBoxSizeMode.Zoom,
        };

        Label title = new()
        {
            Text = $"{AppInfo.Name} {AppInfo.Version}",
            Font = _titleFont,
            AutoSize = true,
            Location = new Point(TextX - 3, 16),
        };

        Label subtitle = new()
        {
            Text = AppInfo.Product,
            AutoSize = true,
            Location = new Point(TextX - 1, 48),
        };

        LinkLabel sourceCode = CreateLink(
            "源代码：GitHub",
            new Point(TextX - 1, 72),
            ("GitHub", "https://github.com/WhiteTeal55/GPGate"));

        Panel separator = new()
        {
            Bounds = new Rectangle(MarginX, 100, TextWidth, 1),
            BackColor = SystemColors.ControlDark,
        };

        LinkLabel attribution = CreateLink(
            "图标素材：GnuPG 官方 artwork · 许可：CC BY-SA 4.0",
            new Point(MarginX, 114),
            ("GnuPG", "https://gnupg.org/"),
            ("CC BY-SA 4.0", "https://creativecommons.org/licenses/by-sa/4.0/"));

        Label disclaimer = new()
        {
            Text = "与 GnuPG 项目无隶属关系。",
            AutoSize = true,
            Location = new Point(MarginX, 142),
        };

        LinkLabel copyright = CreateLink(
            $"{AppInfo.Copyright} · 许可：MIT",
            new Point(MarginX - 1, 174),
            ("MIT", "https://github.com/WhiteTeal55/GPGate/blob/main/LICENSE"));

        Button ok = new()
        {
            Text = "确定",
            Bounds = new Rectangle(292, 184, 88, 32),
        };
        ok.Click += (_, _) => Close();

        AcceptButton = ok;
        CancelButton = ok;

        Controls.AddRange([logo, title, subtitle, sourceCode, separator, attribution, disclaimer, copyright, ok]);

        ResumeLayout(false);
        PerformLayout();
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing); // 先销毁窗口，避免图标提前消失

        if (disposing)
        {
            _titleFont.Dispose();
            _logo.Dispose();
            _icon.Dispose();
        }
    }

    private static LinkLabel CreateLink(string text, Point location, params (string Text, string Url)[] links)
    {
        LinkLabel label = new()
        {
            Text = text,
            AutoSize = true,
            Location = location,
            LinkBehavior = LinkBehavior.HoverUnderline,
            UseMnemonic = false,
        };

        Color linkColor = (Application.IsDarkModeEnabled ? Palette.Dark : Palette.Light).ChipText;
        label.LinkColor = label.ActiveLinkColor = label.VisitedLinkColor = linkColor;

        foreach ((string linkText, string url) in links)
            label.Links.Add(text.IndexOf(linkText, StringComparison.Ordinal), linkText.Length, url);

        label.LinkClicked += (_, e) =>
        {
            if (e.Link?.LinkData is string url)
            {
                using Process? process = Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
            }
        };

        return label;
    }
}
