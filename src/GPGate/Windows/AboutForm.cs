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
        // 每次打开都按当前系统深浅色重新对齐
        Application.SetColorMode(SystemColorMode.System);

        _icon = LoadIcon();
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
        ClientSize = new Size(400, 220);

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

        Panel separator = new()
        {
            Bounds = new Rectangle(MarginX, 86, TextWidth, 1),
            BackColor = SystemColors.ControlDark,
        };

        LinkLabel attribution = new()
        {
            Text = "图标素材：GnuPG 官方 artwork · 许可：CC BY-SA 4.0",
            AutoSize = false,
            Bounds = new Rectangle(MarginX, 100, TextWidth, 20),
            LinkBehavior = LinkBehavior.HoverUnderline,
            UseMnemonic = false,
        };
        Color linkColor = (Application.IsDarkModeEnabled ? Palette.Dark : Palette.Light).ChipText;
        attribution.LinkColor = attribution.ActiveLinkColor = attribution.VisitedLinkColor = linkColor;
        AddLink(attribution, "GnuPG", "https://gnupg.org/");
        AddLink(attribution, "CC BY-SA 4.0", "https://creativecommons.org/licenses/by-sa/4.0/");
        attribution.LinkClicked += (_, e) =>
        {
            if (e.Link?.LinkData is string url)
            {
                using Process? process = Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
            }
        };

        Label disclaimer = new()
        {
            Text = "与 GnuPG 项目无隶属关系。",
            AutoSize = true,
            Location = new Point(MarginX, 128),
        };

        Label copyright = new()
        {
            Text = AppInfo.Copyright,
            AutoSize = true,
            Location = new Point(MarginX - 1, 160),
        };

        Button ok = new()
        {
            Text = "确定",
            Bounds = new Rectangle(292, 172, 88, 32),
        };
        ok.Click += (_, _) => Close();

        AcceptButton = ok;
        CancelButton = ok;

        Controls.AddRange([logo, title, subtitle, separator, attribution, disclaimer, copyright, ok]);

        ResumeLayout(false);
        PerformLayout();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _titleFont.Dispose();
            _logo.Dispose();
            Icon = null; // 先断开引用再释放图标
            _icon.Dispose();
        }

        base.Dispose(disposing);
    }

    private static void AddLink(LinkLabel label, string text, string url) =>
        label.Links.Add(label.Text.IndexOf(text, StringComparison.Ordinal), text.Length, url);

    private static Icon LoadIcon()
    {
        using Stream stream = typeof(AboutForm).Assembly.GetManifestResourceStream("GPGate.app.ico")
            ?? throw new InvalidOperationException("缺少图标资源 GPGate.app.ico");
        return new Icon(stream, new Size(256, 256));
    }
}
