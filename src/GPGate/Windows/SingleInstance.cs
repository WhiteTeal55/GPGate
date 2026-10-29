using System.Runtime.InteropServices;
using System.Text;

namespace GPGate;

// 单实例唤醒：新实例把前台权限转交给已有实例并投递消息，由它自己显示窗口
internal static class SingleInstance
{
    internal static int ShowMessage { get; } = (int)RegisterWindowMessage($"{AppInfo.Name}.ShowWindow");

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

    // 按标题找主窗口（含隐藏的），不比进程名：exe 改名后也能唤醒
    private static IntPtr FindAppWindow()
    {
        IntPtr found = IntPtr.Zero;
        EnumWindows((hwnd, _) =>
        {
            if (WindowFocus.ProcessIdOf(hwnd) == Environment.ProcessId || !IsMainWindow(hwnd))
                return true;

            found = hwnd;
            return false;
        }, IntPtr.Zero);

        return found;
    }

    // 按标题排除同进程的关于窗口
    private static bool IsMainWindow(IntPtr hwnd)
    {
        StringBuilder title = new(64);
        GetWindowText(hwnd, title, title.Capacity);
        return title.ToString() == AppInfo.Name;
    }

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern uint RegisterWindowMessage(string message);

    private delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

    [DllImport("user32.dll")]
    private static extern void EnumWindows(EnumWindowsProc callback, IntPtr lParam);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern void GetWindowText(IntPtr hWnd, StringBuilder text, int maxCount);

    [DllImport("user32.dll")]
    private static extern void PostMessage(IntPtr hWnd, int msg, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll")]
    private static extern bool IsWindowVisible(IntPtr hWnd);
}
