using System.Diagnostics;
using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;

namespace GPGate;

// 托盘常驻的自绘圆角卡片，配色跟随系统深浅色。
internal sealed class MainForm : Form
{
    private static readonly string InfoText = $"v{AppInfo.Version}\n{AppInfo.Copyright}";

    private const int SwRestore = 9;
    private const int WmSysCommand = 0x0112;
    private const int ScMinimize = 0xF020;
    private const int WmNcHitTest = 0x0084;
    private const int WmNcLButtonDblClk = 0x00A3;
    private const int HtCaption = 2;
    private const int DwmwaWindowCornerPreference = 33;
    private const int DwmwcpRound = 2;
    private const int DwmwaBorderColor = 34;

    private const int CardWidth = 300;
    private const int CardHeight = 200;

    private const int VisibleRefreshMs = 2000;
    private const int HiddenRefreshMs = 5000;

    // 设计值都是 96 DPI 下的尺寸，按 _scale 换算
    private float _scale = 1f;
    private Rectangle _tileRect;
    private Rectangle _iconRect;
    private Rectangle _titleRect;
    private Rectangle _subtitleRect;
    private Rectangle _headerRect;

    private readonly Font _titleFont = new("Segoe UI Semibold", 14F);
    private readonly Font _bodyFont = new("Microsoft YaHei UI", 9F);
    private readonly Font _buttonFont = new("Microsoft YaHei UI", 9.5F);

    private readonly FlatButton _start;
    private readonly FlatButton _kill;
    private readonly FlatButton _restart;
    private readonly FlatButton _minimize;
    private readonly FlatButton _close;
    private readonly FlatButton _logButton;
    private readonly FlatButton _info;

    private readonly System.Windows.Forms.Timer _timer = new() { Interval = VisibleRefreshMs };
    private readonly NotifyIcon _tray;
    private readonly TrayMenu _trayMenu = new();
    private readonly AppIcons _icons;

    private Palette _theme = Palette.Dark;
    private bool _lightTheme;
    private bool _running;
    private bool _closeAfterAction;
    private AboutForm? _about;

    // 三个操作按钮一起锁
    private bool Busy => !_start.Enabled;

    private IEnumerable<FlatButton> Buttons => Controls.OfType<FlatButton>();

    public MainForm()
    {
        Text = AppInfo.Name;
        FormBorderStyle = FormBorderStyle.None;
        MaximizeBox = false;
        StartPosition = FormStartPosition.CenterScreen;
        AutoScaleMode = AutoScaleMode.None; // 自己按 DPI 排版
        DoubleBuffered = true;
        KeyPreview = true;

        _start = new FlatButton("启动")
        {
            Font = _buttonFont,
            ForeColor = Color.White,
            Fill = Palette.Accent,
            FillHover = Palette.AccentHover,
            FillPress = Palette.AccentPress,
        };
        _start.Click += (_, _) => RunAction("start");

        _kill = new FlatButton("终止")
        {
            Font = _buttonFont,
            ForeColor = Color.White,
            Fill = Palette.Danger,
            FillHover = Palette.DangerHover,
            FillPress = Palette.DangerPress,
        };
        _kill.Click += (_, _) => RunAction("kill");

        _restart = new FlatButton("重启") { Font = _buttonFont };
        _restart.Click += (_, _) => RunAction("restart");

        _minimize = new FlatButton("最小化") { Glyph = GlyphKind.Minimize };
        _minimize.Click += (_, _) => Hide(); // 直接隐藏，不进 iconic 状态，唤醒时就没有还原动画

        _close = new FlatButton("关闭")
        {
            Glyph = GlyphKind.Close,
            FillHover = Palette.CloseHover,
            FillPress = Palette.ClosePress,
            ForeColorHover = Color.White,
        };
        _close.Click += (_, _) => Close();

        _logButton = new FlatButton("日志") { Font = _bodyFont, AccessibleName = "打开日志文件夹" };
        _logButton.Click += (_, _) => OpenLogFolder();

        _info = new FlatButton(InfoText) { Font = _bodyFont, TextAlign = ContentAlignment.MiddleRight, AccessibleName = "关于" };
        _info.Click += (_, _) => ShowAbout();

        Controls.AddRange([_start, _kill, _restart, _minimize, _close, _logButton, _info]);
        ApplyLayout();

        _icons = new AppIcons(S(48));
        Icon = _icons.Large;

        _tray = new NotifyIcon
        {
            Icon = _icons.Small,
            Text = AppInfo.Name,
            Visible = true,
        };
        _tray.MouseUp += (_, e) =>
        {
            if (_closeAfterAction)
                return;

            if (e.Button == MouseButtons.Right)
                ShowTrayMenu();
            else if (e.Button == MouseButtons.Left)
                ShowMainWindow();
        };

        _timer.Tick += (_, _) => RefreshStatus();
        _timer.Start(); // 常驻，收起后也继续刷新

        ApplyTheme();
        RefreshStatus();
    }

    protected override void OnVisibleChanged(EventArgs e)
    {
        base.OnVisibleChanged(e);

        if (IsDisposed || Disposing)
            return; // 销毁过程中定时器可能已释放

        if (Visible)
            RefreshStatus();

        _timer.Interval = Visible ? VisibleRefreshMs : HiddenRefreshMs;
    }

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);

        SingleInstance.MarkMainWindow(Handle); // 供其他实例定位
        _icons.ApplyWindowIcon(Handle, _running, _lightTheme);
        // Win11 圆角交给 DWM
        int round = DwmwcpRound;
        DwmSetWindowAttribute(Handle, DwmwaWindowCornerPreference, ref round, sizeof(int));
        ApplyDwmBorder();
    }

    // 任务栏按钮出现后再设一次图标
    protected override void OnShown(EventArgs e)
    {
        base.OnShown(e);
        _icons.ApplyWindowIcon(Handle, _running, _lightTheme);
    }

    private void ApplyLayout()
    {
        _scale = SystemDpi() / 96f;
        ClientSize = new Size(S(CardWidth), S(CardHeight));

        _tileRect = R(20, 20, 52, 52);
        _iconRect = R(23, 23, 46, 46);
        _titleRect = R(86, 23, 150, 24);
        _subtitleRect = R(86, 49, 150, 20);
        _headerRect = R(0, 0, CardWidth, 82);

        _start.Bounds = R(20, 138, 260, 40);
        _kill.Bounds = R(20, 138, 124, 40);
        _restart.Bounds = R(156, 138, 124, 40);
        _minimize.Bounds = R(216, 18, 32, 32);
        _close.Bounds = R(252, 18, 32, 32);
        _logButton.Bounds = R(104, 94, 40, 24);
        _info.Bounds = R(152, 89, 128, 34);

        foreach (FlatButton button in Buttons)
        {
            button.DpiScale = _scale;
            button.Radius = button.Glyph == GlyphKind.None ? S(20) : S(8);
        }

        _info.Radius = S(8);
        Invalidate();
    }

    private int S(int value) => (int)Math.Round(value * _scale);

    private Rectangle R(int x, int y, int width, int height) => new(S(x), S(y), S(width), S(height));

    private static int SystemDpi()
    {
        using Graphics g = Graphics.FromHwnd(IntPtr.Zero);
        return (int)Math.Round(g.DpiX);
    }

    private static void OpenLogFolder()
    {
        _ = Directory.CreateDirectory(Log.DirectoryPath);
        using Process? process = Process.Start(new ProcessStartInfo(Log.DirectoryPath) { UseShellExecute = true });
    }

    // 最小化收进托盘
    protected override void OnResize(EventArgs e)
    {
        base.OnResize(e);

        if (WindowState == FormWindowState.Minimized)
            Hide();
    }

    // 执行中先不退出，动作完成后再关；系统关机不拦
    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        if (Busy && e.CloseReason != CloseReason.WindowsShutDown)
        {
            e.Cancel = true;
            _closeAfterAction = true;
            LockUi();
            return;
        }

        if (e.CloseReason == CloseReason.UserClosing && !CloseConfirmation.ConfirmClose(Visible ? this : null))
        {
            e.Cancel = true;
            return;
        }

        base.OnFormClosing(e);
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (e.KeyCode == Keys.Escape)
            Close();
        else if (e.KeyCode == Keys.F1)
            ShowAbout();

        base.OnKeyDown(e);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _timer.Dispose();
            _tray.Dispose(); // 顺带移除托盘图标
            _trayMenu.Dispose();
            Icon = null; // 先断开引用再释放图标
        }

        base.Dispose(disposing); // 子控件销毁后再释放它们引用的字体

        if (disposing)
        {
            _icons.Dispose();
            _titleFont.Dispose();
            _bodyFont.Dispose();
            _buttonFont.Dispose();
        }
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        Graphics g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;

        using (SolidBrush brush = new(_theme.Background))
            g.FillRectangle(brush, ClientRectangle);

        // 图标底色：运行偏蓝，停止中性
        Color from = _running ? Color.FromArgb(58, Palette.Accent) : Color.FromArgb(24, _theme.Text);
        Color to = _running ? Color.FromArgb(14, Palette.Accent) : Color.FromArgb(6, _theme.Text);
        using (GraphicsPath path = Ui.RoundRect(_tileRect, S(14)))
        using (LinearGradientBrush brush = new(_tileRect, from, to, LinearGradientMode.ForwardDiagonal))
            g.FillPath(brush, path);

        Icon icon = _running ? _icons.Big : _lightTheme ? _icons.BigOffLight : _icons.BigOffDark;
        g.DrawIcon(icon, _iconRect);

        TextRenderer.DrawText(g, AppInfo.Name, _titleFont, _titleRect, _theme.Text,
            TextFormatFlags.Left | TextFormatFlags.NoPadding | TextFormatFlags.SingleLine);
        TextRenderer.DrawText(g, AppInfo.Product, _bodyFont, _subtitleRect, _theme.Muted,
            TextFormatFlags.Left | TextFormatFlags.NoPadding | TextFormatFlags.SingleLine);

        DrawStatusChip(g);
    }

    // 边框用 DWM 画才能贴住圆角
    private void ApplyDwmBorder()
    {
        int color = ColorTranslator.ToWin32(_theme.Border);
        DwmSetWindowAttribute(Handle, DwmwaBorderColor, ref color, sizeof(int));
    }

    private void DrawStatusChip(Graphics g)
    {
        string text = _running ? "运行中" : "未运行";
        Color background = _running ? _theme.ChipBackground : _theme.StoppedChipBackground;
        Color foreground = _running ? _theme.ChipText : _theme.StoppedChipText;
        Color dot = _running ? _theme.ChipDot : _theme.StoppedChipDot;

        int pad = S(10), dotSize = S(6), gap = S(6), height = S(28);
        Size textSize = TextRenderer.MeasureText(text, _bodyFont, Size.Empty, TextFormatFlags.NoPadding);
        Rectangle chip = new(S(20), S(92), pad + dotSize + gap + textSize.Width + pad, height);
        int textX = chip.X + pad + dotSize + gap;

        using (GraphicsPath path = Ui.RoundRect(chip, chip.Height / 2))
        using (SolidBrush brush = new(background))
            g.FillPath(brush, path);

        using (SolidBrush brush = new(dot))
            g.FillEllipse(brush, chip.X + pad, chip.Y + (height - dotSize) / 2, dotSize, dotSize);

        TextRenderer.DrawText(g, text, _bodyFont,
            new Rectangle(textX, chip.Y, textSize.Width, chip.Height), foreground,
            TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
    }

    protected override void WndProc(ref Message m)
    {
        // 其他实例请求显示窗口：由本进程显示，跨进程 ShowWindow 会留下半截画面
        if (SingleInstance.ShowMessage != 0 && m.Msg == SingleInstance.ShowMessage)
        {
            ShowMainWindow();
            return;
        }

        // 系统菜单最小化同样只隐藏，不进 iconic
        if (m.Msg == WmSysCommand && ((int)m.WParam & 0xFFF0) == ScMinimize)
        {
            Hide();
            return;
        }

        // 主题切换即时换色，不等下一次轮询
        if (ThemeRefresh.TryApply(this, m, ApplyTheme))
            return;

        if (m.Msg == WmNcLButtonDblClk)
        {
            return; // 双击不做事（无边框窗口会被当成最大化）
        }
        else if (m.Msg == WmNcHitTest)
        {
            // 顶部区域当标题栏，方便拖动
            int lp = (int)m.LParam;
            Point p = PointToClient(new((short)(lp & 0xFFFF), (short)((lp >> 16) & 0xFFFF)));
            if (_headerRect.Contains(p))
            {
                m.Result = HtCaption;
                return;
            }
        }

        base.WndProc(ref m);
    }

    private void ApplyTheme()
    {
        _lightTheme = !Application.IsDarkModeEnabled;
        _theme = _lightTheme ? Palette.Light : Palette.Dark;

        BackColor = _theme.Background;
        foreach (FlatButton button in Buttons)
        {
            button.BackColor = _theme.Background;
            button.FlatAppearance.BorderColor = _theme.Background; // 盖掉自带的 1px 边框
        }

        _restart.Fill = _logButton.Fill = _theme.GhostFill;
        _restart.FillHover = _logButton.FillHover = _minimize.FillHover = _theme.GhostFillHover;
        _restart.FillPress = _logButton.FillPress = _minimize.FillPress = _theme.GhostFill;
        _restart.ForeColor = _theme.Text;
        _minimize.ForeColor = _close.ForeColor = _logButton.ForeColor = _theme.Muted;
        _info.ForeColor = _theme.Muted;
        _info.ForeColorHover = _theme.Text;

        if (IsHandleCreated)
            ApplyDwmBorder();

        ApplyState();
    }

    private void RefreshStatus()
    {
        // null = 查询失败，保持原状态
        if (GpgController.IsRunning() is bool running && running != _running)
        {
            _running = running;
            ApplyState();
        }
    }

    private void ApplyState()
    {
        _tray.Icon = _running ? _icons.Small : _lightTheme ? _icons.OffLightSmall : _icons.OffSmall;
        _tray.Text = _running ? $"{AppInfo.Name}：运行中" : $"{AppInfo.Name}：未运行";

        // 构造期还没建句柄，等 OnHandleCreated / OnShown 再设
        if (IsHandleCreated)
            _icons.ApplyWindowIcon(Handle, _running, _lightTheme);

        _start.Visible = !_running;
        _kill.Visible = _restart.Visible = _running;

        Invalidate();
    }

    // 并发由 ActionGate 保证，这里只管后台执行 + 刷新
    private async void RunAction(string command)
    {
        if (Busy)
            return; // 锁定中丢弃重复点击

        SetBusy(true);
        var (ran, error) = await Task.Run(() => ActionGate.TryRun(command));

        // 命令期间窗口可能已被关闭
        if (IsDisposed || Disposing)
            return;

        SetBusy(false);

        if (_closeAfterAction)
        {
            _closeAfterAction = false;

            // 先解锁：退出确认可能被取消，失败时也要留在界面显示错误
            Enabled = true;
            Cursor = Cursors.Default;

            if (error is null)
            {
                Close();
                return;
            }
        }

        if (!ran)
            return; // 已有命令在跑，静默忽略

        RefreshStatus();

        if (error is not null)
            ShowError(error);
    }

    // 错误提示独立弹出，不把藏在托盘的主窗拉出来
    private void ShowError(string message) =>
        Dialog.Show(Visible ? this : null, TaskDialogIcon.Warning, message);

    private void SetBusy(bool busy) =>
        _start.Enabled = _kill.Enabled = _restart.Enabled = !busy;

    // 已请求关闭：锁住卡片和托盘，等动作结束
    private void LockUi()
    {
        Enabled = false;
        Cursor = Cursors.WaitCursor;
    }

    private void ShowTrayMenu()
    {
        if (_closeAfterAction)
            return;

        RefreshStatus(); // 打开前刷新，菜单项不过期
        _ = Startup.Sync();

        TrayCommand command = _trayMenu.Show(new TrayMenuState(
            WindowVisible: Visible,
            Running: _running,
            Busy: Busy,
            StartupEnabled: Startup.IsEnabled,
            TrayOnStartup: Startup.TrayOnStartup,
            CloseConfirmationEnabled: CloseConfirmation.IsEnabled,
            CanManageStartup: Startup.CanManageStartup,
            LightTheme: _lightTheme));

        switch (command)
        {
            case TrayCommand.ToggleWindow:
                if (Visible)
                    Hide();
                else
                    ShowMainWindow();
                break;
            case TrayCommand.Start: RunAction("start"); break;
            case TrayCommand.Kill: RunAction("kill"); break;
            case TrayCommand.Restart: RunAction("restart"); break;
            case TrayCommand.ToggleStartup:
                if (!Startup.Toggle())
                    ShowError(Startup.ErrorMessage);
                break;
            case TrayCommand.ToggleStartupTray:
                if (!Startup.ToggleTray())
                    ShowError(Startup.ErrorMessage);
                break;
            case TrayCommand.ToggleCloseConfirmation:
                if (!CloseConfirmation.Toggle())
                    ShowError(CloseConfirmation.ErrorMessage);
                break;
            case TrayCommand.OpenLog: OpenLogFolder(); break;
            case TrayCommand.About: ShowAbout(); break;
            case TrayCommand.Close: Close(); break;
        }
    }

    // 关于页独立窗口，不阻塞主界面
    private void ShowAbout()
    {
        if (_about is { IsDisposed: false })
        {
            ActivateAbout();
            return;
        }

        _about = new AboutForm();
        _about.FormClosed += (_, _) => _about = null;
        _about.Show();
        ActivateAbout();

        // 菜单刚收起时前台未必已回到本进程，下一轮消息循环再抢一次
        _ = BeginInvoke(ActivateAbout);
    }

    private void ActivateAbout()
    {
        if (_about is not { IsDisposed: false } about)
            return;

        about.Activate();
        WindowFocus.BringToForeground(about.Handle);
    }

    private void ShowMainWindow()
    {
        _ = Startup.Sync();

        // 外部最小化的窗口靠 SW_RESTORE 显示 + 还原
        ShowWindow(Handle, SwRestore);
        Show(); // 同步 WinForms 的可见状态，否则后面 Hide() 不生效
        Activate();
        WindowFocus.BringToForeground(Handle);
        Refresh(); // 唤回后整窗重绘，避免留下半截画面
    }

    [DllImport("user32.dll")]
    private static extern void ShowWindow(IntPtr hWnd, int nCmdShow);

    [DllImport("dwmapi.dll")]
    private static extern void DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int value, int size);
}