using System.Runtime.InteropServices;

namespace GPGate;

// 单实例唤醒：新实例把前台权限转交给已有实例并投递消息，由它自己显示窗口
internal static class SingleInstance
{
    // 窗口属性跨进程可读，不像标题会撞上同名窗口
    internal const string MainWindowProp = "GPGate.MainWindow";

    internal static int ShowMessage { get; } = (int)RegisterWindowMessage($"{AppInfo.Name}.ShowWindow");

    internal static void MarkMainWindow(IntPtr hwnd) => _ = SetProp(hwnd, MainWindowProp, (IntPtr)1);

    internal static void AllowShowMessageFromLowerIntegrity(IntPtr hwnd)
    {
        ChangeFilterStruct filter = new() { Size = (uint)Marshal.SizeOf<ChangeFilterStruct>() };
        if (!ChangeWindowMessageFilterEx(hwnd, (uint)ShowMessage, MessageFilterAllow, ref filter))
            _ = Log.Warn($"允许低完整性实例唤醒主窗口失败: {Marshal.GetLastWin32Error()}");
    }

    // 找到已有实例的主窗口并请求显示；窗口没真的显示出来也算失败
    internal static bool NotifyExisting()
    {
        IntPtr hwnd = FindAppWindow();
        if (hwnd == IntPtr.Zero)
            return false;

        WindowFocus.AllowSetForeground(WindowFocus.ProcessIdOf(hwnd));
        PostMessage(hwnd, ShowMessage, IntPtr.Zero, IntPtr.Zero);

        // 等它自己显示出来，避免对方卡死时静默退出
        for (int attempt = 0; attempt < 5; attempt++)
        {
            if (IsWindowVisible(hwnd))
                return true;

            Thread.Sleep(100);
        }

        return false;
    }

    // 按标记属性找主窗口（含隐藏的）
    private static IntPtr FindAppWindow()
    {
        IntPtr found = IntPtr.Zero;
        EnumWindows((hwnd, _) =>
        {
            if (WindowFocus.ProcessIdOf(hwnd) == Environment.ProcessId || GetProp(hwnd, MainWindowProp) == IntPtr.Zero)
                return true;

            found = hwnd;
            return false;
        }, IntPtr.Zero);

        return found;
    }

    private const uint MessageFilterAllow = 1;

    [StructLayout(LayoutKind.Sequential)]
    private struct ChangeFilterStruct
    {
        public uint Size;
        public uint ExtStatus;
    }

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern uint RegisterWindowMessage(string message);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool ChangeWindowMessageFilterEx(IntPtr hwnd, uint message, uint action, ref ChangeFilterStruct changeFilterStruct);

    private delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

    [DllImport("user32.dll")]
    private static extern void EnumWindows(EnumWindowsProc callback, IntPtr lParam);

    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern bool SetProp(IntPtr hWnd, string name, IntPtr data);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern IntPtr GetProp(IntPtr hWnd, string name);

    [DllImport("user32.dll")]
    private static extern void PostMessage(IntPtr hWnd, int msg, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll")]
    private static extern bool IsWindowVisible(IntPtr hWnd);
}
