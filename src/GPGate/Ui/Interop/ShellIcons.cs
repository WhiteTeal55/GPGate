using System.Runtime.InteropServices;

namespace GPGate;

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
