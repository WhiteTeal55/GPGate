using System.Diagnostics;

namespace GPGate;

// 托盘宿主：没有主窗口，只有托盘图标、菜单和弹窗
internal sealed class TrayContext : ApplicationContext
{
    private const int StatusRefreshMs = 5000;

    private readonly System.Windows.Forms.Timer _timer = new() { Interval = StatusRefreshMs };
    private readonly NotifyIcon _tray;
    private readonly TrayMenu _trayMenu = new();
    private readonly AppIcons _icons = new();
    private readonly ThemeWatcher _themeWatcher;

    private bool _running;
    private bool _busy;
    private bool _exitAfterAction;
    private bool _confirmingExit;
    private bool _disposed;
    private AboutForm? _about;

    internal TrayContext()
    {
        _tray = new NotifyIcon
        {
            Icon = _icons.Small,
            Text = AppInfo.Name,
            Visible = true,
        };

        _tray.MouseUp += (_, e) =>
        {
            if (!_exitAfterAction && e.Button is MouseButtons.Left or MouseButtons.Right)
                ShowTrayMenu();
        };

        _timer.Tick += (_, _) => RefreshStatus();
        _timer.Start();

        _themeWatcher = new ThemeWatcher(ApplyTrayIcon);

        ApplyTrayIcon();
        RefreshStatus();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing && !_disposed)
        {
            _disposed = true;
            _timer.Dispose();
            _about?.Close();
            _tray.Dispose(); // 顺带移除托盘图标
            _trayMenu.Dispose();
            _themeWatcher.Dispose();
            _icons.Dispose();
        }

        base.Dispose(disposing);
    }

    private void ApplyTrayIcon()
    {
        bool light = !Application.IsDarkModeEnabled;
        _tray.Icon = _busy || _running ? _icons.Small : light ? _icons.OffLightSmall : _icons.OffSmall;
        _tray.Text = _busy ? $"{AppInfo.Name}：处理中"
            : _running ? $"{AppInfo.Name}：运行中"
            : $"{AppInfo.Name}：未运行";
    }

    private void RefreshStatus()
    {
        // null = 查询失败，保持原状态
        if (GpgController.IsRunning() is bool running && running != _running)
        {
            _running = running;
            ApplyTrayIcon();
        }
    }

    private async void RunAction(string command)
    {
        if (_busy)
            return;

        SetBusy(true);

        (bool Ran, string? Error) outcome;
        try
        {
            outcome = await Task.Run(() => ActionGate.TryRun(command));
        }
        catch (Exception ex)
        {
            outcome = (true, Log.Error($"执行 {command} 出错: {ex.Message}"));
        }

        if (_disposed)
            return;

        SetBusy(false);
        var (ran, error) = outcome;

        if (_exitAfterAction)
        {
            _exitAfterAction = false;

            // 退出请求优先：即使本次没抢到动作锁也继续退出
            if (error is null || !ran)
            {
                Exit();
                return;
            }
        }

        if (!ran)
        {
            if (error is not null)
                ShowError(error);
            return;
        }

        RefreshStatus();

        if (error is not null)
            ShowError(error);
    }

    private void SetBusy(bool busy)
    {
        _busy = busy;
        ApplyTrayIcon();
    }

    private void Exit()
    {
        if (_busy)
        {
            _exitAfterAction = true;
            return;
        }

        if (_confirmingExit)
            return;

        _confirmingExit = true;
        try
        {
            if (!CloseConfirmation.ConfirmClose(null))
                return;
        }
        finally
        {
            _confirmingExit = false;
        }

        _tray.Visible = false;
        _ = Log.Info("退出托盘");
        ExitThread();
    }

    private void ShowTrayMenu()
    {
        if (_exitAfterAction)
            return;

        RefreshStatus();
        _ = Startup.Sync();

        TrayCommand command = _trayMenu.Show(new TrayMenuState(
            Running: _running,
            Busy: _busy,
            StartupEnabled: Startup.IsEnabled,
            TrayOnStartup: Startup.TrayOnStartup,
            CloseConfirmationEnabled: CloseConfirmation.IsEnabled,
            CanManageStartup: Startup.CanManageStartup,
            LightTheme: !Application.IsDarkModeEnabled));

        switch (command)
        {
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
            case TrayCommand.Close: Exit(); break;
        }
    }

    private static void ShowError(string message) => Dialog.ShowResult(message);

    private static void OpenLogFolder()
    {
        try
        {
            _ = Directory.CreateDirectory(Log.DirectoryPath);
            Process.Start(new ProcessStartInfo(Log.DirectoryPath) { UseShellExecute = true })?.Dispose();
        }
        catch (Exception ex)
        {
            _ = Log.Warn($"打开日志文件夹失败: {ex.Message}");
            ShowError("无法打开日志文件夹。");
        }
    }

    // 独立窗口，不阻塞托盘
    private void ShowAbout()
    {
        try
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
        }
        catch (Exception ex)
        {
            _about = null;
            _ = Log.Warn($"打开关于窗口失败: {ex.Message}");
            ShowError($"无法打开关于窗口：{ex.Message}");
        }
    }

    private void ActivateAbout()
    {
        if (_about is not { IsDisposed: false } about)
            return;

        about.Activate();
        WindowFocus.BringToForeground(about.Handle);
    }

    // 用隐藏窗口接收系统主题广播
    private sealed class ThemeWatcher : NativeWindow, IDisposable
    {
        private readonly Action _onThemeChanged;

        internal ThemeWatcher(Action onThemeChanged)
        {
            _onThemeChanged = onThemeChanged;
            CreateHandle(new CreateParams { Caption = $"{AppInfo.Name}.Theme" });
        }

        public void Dispose() => DestroyHandle();

        protected override void WndProc(ref Message m)
        {
            if (ThemeRefresh.TryApply(m))
            {
                _onThemeChanged();
                return;
            }

            base.WndProc(ref m);
        }
    }
}
