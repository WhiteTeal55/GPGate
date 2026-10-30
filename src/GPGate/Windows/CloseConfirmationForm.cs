namespace GPGate;

// 退出确认：标准控件 + 系统配色
internal sealed class CloseConfirmationForm : Form
{
    private readonly Icon _icon;
    private readonly Bitmap? _promptIcon;
    private readonly CheckBox _dontAskAgain;

    internal CloseConfirmationForm()
    {
        SuspendLayout();

        // 每次打开都按系统深浅色重设
        Application.SetColorMode(SystemColorMode.System);

        _icon = AppInfo.LoadAppIcon();
        _promptIcon = ShellIcons.Load(ShellIcons.Info);

        Text = AppInfo.Name;
        Icon = _icon;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        ShowInTaskbar = false;
        StartPosition = FormStartPosition.CenterParent;
        AutoScaleDimensions = new SizeF(96F, 96F);
        AutoScaleMode = AutoScaleMode.Dpi;
        ClientSize = new Size(360, 126);

        PictureBox logo = new()
        {
            Bounds = new Rectangle(24, 18, 32, 32),
            Image = _promptIcon,
            SizeMode = PictureBoxSizeMode.CenterImage,
        };

        Label prompt = new()
        {
            Text = "是否退出此软件？",
            AutoSize = true,
            Location = new Point(72, 24),
        };

        Panel separator = new()
        {
            Bounds = new Rectangle(0, 70, 360, 1),
            BackColor = SystemColors.ControlDark,
        };

        _dontAskAgain = new CheckBox
        {
            Text = "不再提示",
            AutoSize = true,
            Location = new Point(24, 88),
        };

        Button cancel = new()
        {
            Text = "取消",
            DialogResult = DialogResult.No,
            Bounds = new Rectangle(148, 82, 88, 32),
        };

        Button exit = new()
        {
            Text = "确定",
            DialogResult = DialogResult.Yes,
            Bounds = new Rectangle(248, 82, 88, 32),
        };

        AcceptButton = exit;
        CancelButton = cancel;
        Controls.AddRange([logo, prompt, separator, _dontAskAgain, cancel, exit]);
        ResumeLayout(false);
        PerformLayout();

        Shown += (_, _) => ActiveControl = exit; // 默认焦点给确定
    }

    internal bool DontAskAgain => _dontAskAgain.Checked;

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing); // 先销毁窗口再释放图标

        if (disposing)
        {
            _promptIcon?.Dispose();
            _icon.Dispose();
        }
    }
}
