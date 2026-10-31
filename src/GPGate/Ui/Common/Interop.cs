using System.Runtime.InteropServices;

namespace GPGate;

// 托盘图标：运行状态与系统配色对应不同资源
internal sealed class AppIcons : IDisposable
{
    internal Icon Small { get; } = Load(AppInfo.IconResource);
    internal Icon OffSmall { get; } = Load(AppInfo.OffIconResource);
    internal Icon OffLightSmall { get; } = Load(AppInfo.OffLightIconResource);

    public void Dispose()
    {
        Small.Dispose();
        OffSmall.Dispose();
        OffLightSmall.Dispose();
    }

    private static Icon Load(string name)
    {
        using Stream stream = typeof(AppIcons).Assembly.GetManifestResourceStream(name)
            ?? throw new InvalidOperationException($"缺少图标资源 {name}");
        return new Icon(stream, SystemInformation.SmallIconSize);
    }
}

// Win11 扁平系统图标（shell stock）
internal static class ShellIcons
{
    private const uint StockIconFlags = 0x104; // SHGSI_ICON | SHGSI_SHELLICONSIZE

    internal const uint Application = 2;
    internal const uint Warning = 78;
    internal const uint Info = 79;
    internal const uint Error = 80;

    // 调用方负责释放；系统不提供时返回 null
    internal static Bitmap? Load(uint siid)
    {
        SHSTOCKICONINFO info = new() { cbSize = (uint)Marshal.SizeOf<SHSTOCKICONINFO>() };
        if (SHGetStockIconInfo(siid, StockIconFlags, ref info) != 0 || info.hIcon == IntPtr.Zero)
            return null;

        try
        {
            using Icon icon = Icon.FromHandle(info.hIcon);
            return icon.ToBitmap();
        }
        finally
        {
            _ = DestroyIcon(info.hIcon);
        }
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct SHSTOCKICONINFO
    {
        public uint cbSize;
        public IntPtr hIcon;
        public int iSysImageIndex;
        public int iIcon;

        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)]
        public string szPath;
    }

    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    private static extern int SHGetStockIconInfo(uint siid, uint flags, ref SHSTOCKICONINFO info);

    [DllImport("user32.dll")]
    private static extern bool DestroyIcon(IntPtr handle);
}

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

    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll")]
    private static extern void SetForegroundWindow(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern bool AttachThreadInput(uint idAttach, uint idAttachTo, bool attach);

    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint processId);

    [DllImport("kernel32.dll")]
    private static extern uint GetCurrentThreadId();
}
