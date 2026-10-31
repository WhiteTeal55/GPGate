using System.Runtime.InteropServices;

namespace GPGate;

internal enum TrayCommand
{
    None,
    Start,
    Kill,
    Restart,
    ToggleStartup,
    ToggleStartupNotification,
    OpenLog,
    About,
    Close,
}

internal readonly record struct TrayMenuState(
    bool Running,
    bool Busy,
    bool StartupEnabled,
    bool StartupNotificationEnabled,
    bool CanManageStartup,
    bool LightTheme);

// 托盘右键菜单：系统原生 popup，自己持有隐藏 owner 窗口
internal sealed class TrayMenu : NativeWindow, IDisposable
{
    private const int WmNull = 0x0000;
    private const int WsPopup = unchecked((int)0x80000000);
    private const int WsExToolWindow = 0x00000080;

    private const uint MfString = 0x0000;
    private const uint MfGrayed = 0x0001;
    private const uint MfChecked = 0x0008;
    private const uint MfSeparator = 0x0800;
    private const uint TpmRightButton = 0x0002;
    private const uint TpmReturnCmd = 0x0100;

    private const int PreferredAppModeForceDark = 2;
    private const int PreferredAppModeForceLight = 3;

    internal TrayMenu()
    {
        // 不要指定 ClassName="STATIC"：会污染 WinForms 类缓存，Label 建句柄报 1410
        CreateHandle(new CreateParams
        {
            Caption = $"{AppInfo.Name}.MenuOwner",
            Style = WsPopup,
            ExStyle = WsExToolWindow,
        });
    }

    // 弹出并等待选择，调用方按返回值分发
    internal TrayCommand Show(TrayMenuState state)
    {
        IntPtr menu = CreatePopupMenu();
        if (menu == IntPtr.Zero)
            return TrayCommand.None;

        try
        {
            bool canAct = !state.Busy;
            if (state.Running)
            {
                Append(menu, TrayCommand.Kill, "终止", enabled: canAct);
                Append(menu, TrayCommand.Restart, "重启", enabled: canAct);
            }
            else
            {
                Append(menu, TrayCommand.Start, "启动", enabled: canAct);
            }

            Separator(menu);
            bool canStartup = state.CanManageStartup;
            Append(menu, TrayCommand.ToggleStartup, "开机自启",
                enabled: canStartup, checkedItem: state.StartupEnabled);
            if (!canStartup)
                Append(menu, TrayCommand.None, "开机自启需要管理员权限", enabled: false);
            Append(menu, TrayCommand.ToggleStartupNotification, "启动时提示",
                checkedItem: state.StartupNotificationEnabled);
            Separator(menu);
            Append(menu, TrayCommand.OpenLog, "打开日志");
            Append(menu, TrayCommand.About, "关于");
            Append(menu, TrayCommand.Close, "退出");

            GetCursorPos(out CursorPoint cursor);

            // 用隐藏 owner 当前台：菜单自动收起且不显示主窗口
            ApplyTheme(state.LightTheme);
            WindowFocus.BringToForeground(Handle);
            int command = TrackPopupMenuEx(menu, TpmRightButton | TpmReturnCmd,
                cursor.X, cursor.Y, Handle, IntPtr.Zero);
            PostMessage(Handle, WmNull, IntPtr.Zero, IntPtr.Zero);

            return (TrayCommand)command;
        }
        finally
        {
            DestroyMenu(menu);
        }
    }

    public void Dispose() => DestroyHandle();

    private static void Append(IntPtr menu, TrayCommand command, string text,
        bool enabled = true, bool checkedItem = false)
    {
        uint flags = MfString | (enabled ? 0 : MfGrayed) | (checkedItem ? MfChecked : 0);
        AppendMenu(menu, flags, (int)command, text);
    }

    private static void Separator(IntPtr menu) => AppendMenu(menu, MfSeparator, 0, null);

    // 托盘菜单跟随 Windows 应用配色
    private void ApplyTheme(bool lightTheme)
    {
        try
        {
            SetPreferredAppMode(lightTheme ? PreferredAppModeForceLight : PreferredAppModeForceDark);
            FlushMenuThemes();
            AllowDarkModeForWindow(Handle, !lightTheme);
        }
        catch (Exception ex) when (ex is EntryPointNotFoundException or DllNotFoundException)
        {
            // 系统不提供这些入口，菜单用默认配色
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct CursorPoint
    {
        public int X;
        public int Y;
    }

    [DllImport("user32.dll")]
    private static extern IntPtr CreatePopupMenu();

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern void AppendMenu(IntPtr hMenu, uint uFlags, nint uIDNewItem, string? lpNewItem);

    [DllImport("user32.dll")]
    private static extern int TrackPopupMenuEx(IntPtr hMenu, uint fuFlags, int x, int y, IntPtr hWnd, IntPtr lptpm);

    [DllImport("user32.dll")]
    private static extern void DestroyMenu(IntPtr hMenu);

    [DllImport("user32.dll")]
    private static extern void GetCursorPos(out CursorPoint point);

    [DllImport("user32.dll")]
    private static extern void PostMessage(IntPtr hWnd, int msg, IntPtr wParam, IntPtr lParam);

    [DllImport("uxtheme.dll", EntryPoint = "#132")]
    private static extern void AllowDarkModeForWindow(IntPtr hWnd, bool allow);

    [DllImport("uxtheme.dll", EntryPoint = "#135")]
    private static extern void SetPreferredAppMode(int mode);

    [DllImport("uxtheme.dll", EntryPoint = "#136")]
    private static extern void FlushMenuThemes();
}
