using System.Runtime.InteropServices;

namespace GPGate;

// Activate() 会被前台锁忽略，临时挂到前台线程再抢一次
internal static class WindowFocus
{
    internal static void BringToForeground(IntPtr hwnd)
    {
        IntPtr foreground = GetForegroundWindow();
        if (foreground == hwnd)
            return;

        uint foregroundThread = GetWindowThreadProcessId(foreground, out _);
        uint selfThread = GetCurrentThreadId();
        bool attached = AttachThreadInput(selfThread, foregroundThread, true);
        SetForegroundWindow(hwnd);

        if (attached)
            _ = AttachThreadInput(selfThread, foregroundThread, false);
    }

    internal static uint ProcessIdOf(IntPtr hwnd)
    {
        _ = GetWindowThreadProcessId(hwnd, out uint processId);
        return processId;
    }

    // 把前台权限转交给指定进程
    internal static void AllowSetForeground(uint processId) => AllowSetForegroundWindow(processId);

    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll")]
    private static extern void SetForegroundWindow(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern void AllowSetForegroundWindow(uint processId);

    [DllImport("user32.dll")]
    private static extern bool AttachThreadInput(uint idAttach, uint idAttachTo, bool attach);

    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint processId);

    [DllImport("kernel32.dll")]
    private static extern uint GetCurrentThreadId();
}
