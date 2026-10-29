namespace GPGate;

internal sealed class MessageDialogForm : Form
{
    private const int DwmwaUseImmersiveDarkMode = 20;
    private const int DialogWidth = 520;
    private const int ContentLeft = 76;
    private const int ContentWidth = DialogWidth - ContentLeft - 24;
    private const int ButtonHeight = 32;

    private readonly bool _darkTheme;
    private readonly Palette _theme;
    private readonly string? _details;
    private readonly Label? _detailsLink;
    private readonly TextBox? _detailsText;
    private readonly Button _ok;
    private readonly int _collapsedHeight;
    private readonly int _collapsedButtonTop;

    internal MessageDialogForm(TaskDialogIcon icon, string text, string? heading, string? details)
    {
        _darkTheme = Application.IsDarkModeEnabled;
        _theme = _darkTheme ? Palette.Dark : Palette.Light;
        _details = details;

        Text = AppInfo.Name;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        ShowInTaskbar = false;
        StartPosition = FormStartPosition.CenterParent;
        AutoScaleDimensions = new SizeF(96F, 96F);
        AutoScaleMode = AutoScaleMode.Dpi;
        BackColor = _theme.Background;
        ForeColor = _theme.Text;

        int y = 22;
        if (icon != TaskDialogIcon.None)
        {
            PictureBox iconBox = new()
            {
                Image = GetIcon(icon).ToBitmap(),
                SizeMode = PictureBoxSizeMode.CenterImage,
                Bounds = new Rectangle(12, y - 8, 48, 48),
            };
            Controls.Add(iconBox);
        }

        if (!string.IsNullOrWhiteSpace(heading))
        {
            Label headingLabel = new()
            {
                Text = heading,
                Font = new Font("Segoe UI Semibold", 10F),
                ForeColor = _theme.Text,
                BackColor = _theme.Background,
                Bounds = new Rectangle(ContentLeft, y, ContentWidth, 24),
            };
            Controls.Add(headingLabel);
            y += 30;
        }

        Font messageFont = SystemFonts.MessageBoxFont ?? SystemFonts.DefaultFont;
        Size textSize = Measure(text, messageFont, ContentWidth);
        Label message = new()
        {
            Text = text,
            Font = messageFont,
            ForeColor = _theme.Text,
            BackColor = _theme.Background,
            Bounds = new Rectangle(ContentLeft, y, ContentWidth, textSize.Height),
        };
        Controls.Add(message);
        y += textSize.Height;

        if (details is not null)
        {
            _detailsLink = new Label
            {
                Text = "显示详细信息",
                AutoSize = true,
                Cursor = Cursors.Hand,
                ForeColor = _theme.ChipText,
                BackColor = _theme.Background,
                Location = new Point(ContentLeft, y + 10),
            };
            _detailsLink.Click += (_, _) => ToggleDetails();
            Controls.Add(_detailsLink);

            _detailsText = new TextBox
            {
                Multiline = true,
                ReadOnly = true,
                ScrollBars = ScrollBars.Vertical,
                WordWrap = true,
                BorderStyle = BorderStyle.FixedSingle,
                BackColor = _theme.GhostFill,
                ForeColor = _theme.Text,
                Bounds = new Rectangle(ContentLeft, y + 34, ContentWidth, 110),
                Visible = false,
                Text = details,
            };
            Controls.Add(_detailsText);
            y += 30;
        }
        _ok = new Button
        {
            Text = "确定",
            DialogResult = DialogResult.OK,
            Bounds = new Rectangle(DialogWidth - 24 - 88, y + 20, 88, ButtonHeight),
            FlatStyle = FlatStyle.Flat,
            BackColor = _theme.GhostFill,
            ForeColor = _theme.Text,
            UseVisualStyleBackColor = false,
        };
        _ok.FlatAppearance.BorderColor = _theme.Border;
        _ok.FlatAppearance.MouseOverBackColor = _theme.GhostFillHover;
        _ok.FlatAppearance.MouseDownBackColor = _theme.GhostFill;
        Controls.Add(_ok);

        AcceptButton = _ok;
        CancelButton = _ok;
        _collapsedButtonTop = _ok.Top;
        _collapsedHeight = _ok.Bottom + 18;
        ClientSize = new Size(DialogWidth, _collapsedHeight);
    }

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);

        int darkTheme = _darkTheme ? 1 : 0;
        _ = DwmSetWindowAttribute(Handle, DwmwaUseImmersiveDarkMode, ref darkTheme, sizeof(int));
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            foreach (Control control in Controls)
            {
                if (control is PictureBox { Image: { } image })
                    image.Dispose();
            }
        }

        base.Dispose(disposing);
    }

    private void ToggleDetails()
    {
        if (_detailsLink is null || _detailsText is null)
            return;

        bool show = !_detailsText.Visible;
        _detailsText.Visible = show;
        _detailsLink.Text = show ? "隐藏详细信息" : "显示详细信息";
        _ok.Top = _collapsedButtonTop + (show ? _detailsText.Height + 12 : 0);
        ClientSize = new Size(DialogWidth, show ? _ok.Bottom + 18 : _collapsedHeight);
        if (show)
            _detailsText.BringToFront();
    }

    private static Size Measure(string text, Font font, int width) =>
        TextRenderer.MeasureText(text, font, new Size(width, int.MaxValue),
            TextFormatFlags.WordBreak | TextFormatFlags.TextBoxControl | TextFormatFlags.NoPadding);

    private static Icon GetIcon(TaskDialogIcon icon)
    {
        if (icon == TaskDialogIcon.Error)
            return SystemIcons.Error;
        if (icon == TaskDialogIcon.Warning)
            return SystemIcons.Warning;
        if (icon == TaskDialogIcon.Information)
            return SystemIcons.Information;
        return SystemIcons.Application;
    }

    [System.Runtime.InteropServices.DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int value, int size);
}
