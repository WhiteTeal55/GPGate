using System.Media;
using System.Runtime.InteropServices;

namespace GPGate;

// 托盘图标：运行状态与系统配色对应不同资源
internal sealed class AppIcons : IDisposable
{
    private readonly List<Icon> _owned = [];

    internal AppIcons()
    {
        Size small = SystemInformation.SmallIconSize;
        Small = Load(AppInfo.IconResource, small);
        OffSmall = Load(AppInfo.OffIconResource, small);
        OffLightSmall = Load(AppInfo.OffLightIconResource, small);
    }

    internal Icon Small { get; }
    internal Icon OffSmall { get; }
    internal Icon OffLightSmall { get; }

    public void Dispose()
    {
        foreach (Icon icon in _owned)
            icon.Dispose();
    }

    // 只记录自己创建的图标，Dispose 时统一释放
    private Icon Load(string name, Size size)
    {
        using Stream stream = typeof(AppIcons).Assembly.GetManifestResourceStream(name)
            ?? throw new InvalidOperationException($"缺少图标资源 {name}");

        Icon icon = new(stream, size);
        _owned.Add(icon);
        return icon;
    }
}

// 新版系统提示图标（Win11 扁平样式），取自 shell 的 Stock 图标
internal static class ShellIcons
{
    private const uint StockIconFlags = 0x104; // SHGSI_ICON | SHGSI_SHELLICONSIZE

    internal const uint Application = 2;
    internal const uint Warning = 78;
    internal const uint Info = 79;
    internal const uint Error = 80;

    // 加载图标位图，调用方负责释放；系统不提供时返回 null
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

internal static class WindowAttention
{
    private const uint FlashwTray = 0x00000002;
    private const uint FlashwTimerNoForeground = 0x0000000C;

    internal static void Notify(Form form, SystemSound sound)
    {
        sound.Play();

        Form target = form.ShowInTaskbar ? form : form.Owner ?? form;
        FlashWindowInfo info = new()
        {
            Size = (uint)Marshal.SizeOf<FlashWindowInfo>(),
            Handle = target.Handle,
            Flags = FlashwTray | FlashwTimerNoForeground,
        };
        _ = FlashWindowEx(ref info);
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct FlashWindowInfo
    {
        internal uint Size;
        internal IntPtr Handle;
        internal uint Flags;
        internal uint Count;
        internal uint Timeout;
    }

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool FlashWindowEx(ref FlashWindowInfo info);
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
