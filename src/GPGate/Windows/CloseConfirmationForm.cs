namespace GPGate;

internal sealed class CloseConfirmationForm : Form
{
    private const int DwmwaUseImmersiveDarkMode = 20;

    private readonly CheckBox _dontAskAgain;
    private readonly bool _darkTheme;

    internal CloseConfirmationForm(bool lightTheme)
    {
        Palette theme = lightTheme ? Palette.Light : Palette.Dark;
        _darkTheme = !lightTheme;

        Text = AppInfo.Name;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        ShowInTaskbar = false;
        StartPosition = FormStartPosition.CenterParent;
        AutoScaleDimensions = new SizeF(96F, 96F);
        AutoScaleMode = AutoScaleMode.Dpi;
        ClientSize = new Size(360, 156);
        BackColor = theme.Background;
        ForeColor = theme.Text;

        Label prompt = new()
        {
            Text = "是否退出此软件？",
            AutoSize = true,
            Location = new Point(24, 24),
            BackColor = theme.Background,
            ForeColor = theme.Text,
        };

        _dontAskAgain = new CheckBox
        {
            Text = "不再提示",
            AutoSize = true,
            Location = new Point(40, 66),
            BackColor = theme.Background,
            ForeColor = theme.Text,
            FlatStyle = FlatStyle.Flat,
        };

        Button cancel = CreateButton("取消", new Point(152, 108), theme);
        cancel.DialogResult = DialogResult.No;

        Button exit = CreateButton("退出", new Point(252, 108), theme);
        exit.DialogResult = DialogResult.Yes;

        AcceptButton = exit;
        CancelButton = cancel;
        Controls.AddRange([prompt, _dontAskAgain, cancel, exit]);
    }

    internal bool DontAskAgain => _dontAskAgain.Checked;

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);

        int darkTheme = _darkTheme ? 1 : 0;
        _ = DwmSetWindowAttribute(Handle, DwmwaUseImmersiveDarkMode, ref darkTheme, sizeof(int));
    }

    private static Button CreateButton(string text, Point location, Palette theme)
    {
        Button button = new()
        {
            Text = text,
            Bounds = new Rectangle(location, new Size(88, 30)),
            FlatStyle = FlatStyle.Flat,
            BackColor = theme.GhostFill,
            ForeColor = theme.Text,
            UseVisualStyleBackColor = false,
        };
        button.FlatAppearance.BorderColor = theme.Border;
        button.FlatAppearance.MouseOverBackColor = theme.GhostFillHover;
        button.FlatAppearance.MouseDownBackColor = theme.GhostFill;
        return button;
    }

    [System.Runtime.InteropServices.DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int value, int size);
}
