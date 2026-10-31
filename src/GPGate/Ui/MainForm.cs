using System.Diagnostics;

namespace GPGate;

// 隐藏的托盘宿主，不显示主窗口。
internal sealed class MainForm : Form
{
    private const int StatusRefreshMs = 5000;

    private readonly System.Windows.Forms.Timer _timer = new() { Interval = StatusRefreshMs };
    private readonly NotifyIcon _tray;
    private readonly TrayMenu _trayMenu = new();
    private readonly AppIcons _icons = new();

    private bool _lightTheme;
    private bool _running;
    private bool _busy;
    private bool _closeAfterAction;
    private AboutForm? _about;

    internal MainForm()
    {
        Text = AppInfo.Name;
        ShowInTaskbar = false;
        ShowIcon = false;
        FormBorderStyle = FormBorderStyle.FixedToolWindow;
        StartPosition = FormStartPosition.Manual;

        _tray = new NotifyIcon
        {
            Icon = _icons.Small,
            Text = AppInfo.Name,
            Visible = true,
        };
        if (StartupNotification.IsEnabled)
            _tray.ShowBalloonTip(5000, AppInfo.Name, "程序已在系统托盘运行。", ToolTipIcon.Info);
        _tray.MouseUp += (_, e) =>
        {
            if (!_closeAfterAction && e.Button is MouseButtons.Left or MouseButtons.Right)
                ShowTrayMenu();
        };

        _timer.Tick += (_, _) => RefreshStatus();
        _timer.Start();

        ApplyTheme();
        RefreshStatus();
        _ = Handle;
    }

    internal void ApplyTheme()
    {
        _lightTheme = !Application.IsDarkModeEnabled;
        ApplyTrayIcon();
    }

    protected override void SetVisibleCore(bool value)
    {
        // Application.Run owns this form, but the tray-only app has no main window.
        base.SetVisibleCore(false);
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        if (_busy && e.CloseReason != CloseReason.WindowsShutDown)
        {
            e.Cancel = true;
            _closeAfterAction = true;
            return;
        }

        base.OnFormClosing(e);
    }

    protected override void WndProc(ref Message m)
    {
        if (ThemeRefresh.TryApply(this, m, ApplyTheme))
            return;

        base.WndProc(ref m);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _timer.Dispose();
            _tray.Dispose();
            _trayMenu.Dispose();
            _icons.Dispose();
        }

        base.Dispose(disposing);
    }

    private static void OpenLogFolder()
    {
        _ = Directory.CreateDirectory(Log.DirectoryPath);
        using Process? process = Process.Start(new ProcessStartInfo(Log.DirectoryPath) { UseShellExecute = true });
    }

    private void RefreshStatus()
    {
        if (GpgController.IsRunning() is bool running && running != _running)
        {
            _running = running;
            ApplyTrayIcon();
        }
    }

    private void ApplyTrayIcon()
    {
        _tray.Icon = _running ? _icons.Small : _lightTheme ? _icons.OffLightSmall : _icons.OffSmall;
        _tray.Text = _running ? $"{AppInfo.Name}：运行中" : $"{AppInfo.Name}：未运行";
    }

    private async void RunAction(string command)
    {
        if (_busy)
            return;

        _busy = true;
        var (ran, error) = await Task.Run(() => ActionGate.TryRun(command));

        if (IsDisposed || Disposing)
            return;

        _busy = false;

        if (_closeAfterAction)
        {
            _closeAfterAction = false;
            if (error is null)
            {
                Close();
                return;
            }
        }

        if (!ran)
            return;

        RefreshStatus();

        if (error is not null)
            ShowError(error);
    }

    private static void ShowError(string message) =>
        Dialog.Show(null, TaskDialogIcon.Warning, message);

    private void ShowTrayMenu()
    {
        if (_closeAfterAction)
            return;

        RefreshStatus();
        TrayCommand command = _trayMenu.Show(new TrayMenuState(
            Running: _running,
            Busy: _busy,
            StartupEnabled: Startup.IsEnabled,
            StartupNotificationEnabled: StartupNotification.IsEnabled,
            CanManageStartup: Startup.CanManageStartup,
            LightTheme: _lightTheme));

        switch (command)
        {
            case TrayCommand.Start: RunAction("start"); break;
            case TrayCommand.Kill: RunAction("kill"); break;
            case TrayCommand.Restart: RunAction("restart"); break;
            case TrayCommand.ToggleStartup:
                if (!Startup.Toggle())
                    ShowError(Startup.ErrorMessage);
                break;
            case TrayCommand.ToggleStartupNotification:
                if (!StartupNotification.Toggle())
                    ShowError(StartupNotification.ErrorMessage);
                break;
            case TrayCommand.OpenLog: OpenLogFolder(); break;
            case TrayCommand.About: ShowAbout(); break;
            case TrayCommand.Close: Close(); break;
        }
    }

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
        _ = BeginInvoke(ActivateAbout);
    }

    private void ActivateAbout()
    {
        if (_about is not { IsDisposed: false } about)
            return;

        about.Activate();
        WindowFocus.BringToForeground(about.Handle);
    }

}
