using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;

namespace GPGate;

// 图标：托盘用的小图标，以及右上角带状态圆点的窗口/任务栏图标
internal sealed class AppIcons : IDisposable
{
    private const int WmSetIcon = 0x0080;

    // 状态圆点相对图标宽度的比例
    private const float DotScale = 0.41f;
    private const float MarginScale = 0.02f;
    private const float RingScale = 0.05f;

    private readonly List<Icon> _owned = [];
    private Icon? _windowSmall;
    private Icon? _windowLarge;

    internal AppIcons(int bigSize)
    {
        Size small = SystemInformation.SmallIconSize;
        Size big = new(bigSize, bigSize);
        Small = Load(AppInfo.IconResource, small);
        Large = Load(AppInfo.IconResource, SystemInformation.IconSize);
        OffSmall = Load(AppInfo.OffIconResource, small);
        OffLightSmall = Load(AppInfo.OffLightIconResource, small);
        Big = Load(AppInfo.IconResource, big);
        BigOffDark = Load(AppInfo.OffIconResource, big);
        BigOffLight = Load(AppInfo.OffLightIconResource, big);
    }

    internal Icon Small { get; }
    internal Icon Large { get; }
    internal Icon OffSmall { get; }
    internal Icon OffLightSmall { get; }
    internal Icon Big { get; }
    internal Icon BigOffDark { get; }
    internal Icon BigOffLight { get; }

    public void Dispose()
    {
        foreach (Icon icon in _owned)
            icon.Dispose();

        _windowSmall?.Dispose();
        _windowLarge?.Dispose();
    }

    // 窗口与任务栏图标：右上角叠状态圆点，描边按主题取色
    internal void ApplyWindowIcon(IntPtr hwnd, bool running, bool lightTheme)
    {
        if (hwnd == IntPtr.Zero)
            return;

        Color dot = running ? Palette.Accent : lightTheme ? Palette.Light.Muted : Palette.Dark.Muted;
        Color ring = lightTheme ? Color.FromArgb(70, 0, 0, 0) : Color.FromArgb(235, 255, 255, 255);

        Icon small = Compose(Small, dot, ring);
        Icon large = Compose(Large, dot, ring);

        SendMessage(hwnd, WmSetIcon, IntPtr.Zero, small.Handle);
        SendMessage(hwnd, WmSetIcon, (IntPtr)1, large.Handle);

        // 先换新图标再释放旧的，避免窗口短暂持有已销毁的句柄
        _windowSmall?.Dispose();
        _windowLarge?.Dispose();
        _windowSmall = small;
        _windowLarge = large;
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

    // 把圆点画在图标右上角，再转成窗口图标
    private static Icon Compose(Icon source, Color fill, Color ring)
    {
        using Bitmap bitmap = source.ToBitmap();
        using Graphics g = Graphics.FromImage(bitmap);
        g.SmoothingMode = SmoothingMode.AntiAlias;

        float diameter = bitmap.Width * DotScale;
        float margin = bitmap.Width * MarginScale;
        RectangleF dot = new(bitmap.Width - diameter - margin, margin, diameter, diameter);

        using (SolidBrush brush = new(fill))
            g.FillEllipse(brush, dot);
        using (Pen pen = new(ring, Math.Max(1f, bitmap.Width * RingScale)))
            g.DrawEllipse(pen, dot);

        IntPtr handle = bitmap.GetHicon();
        try
        {
            using Icon temp = Icon.FromHandle(handle);
            return (Icon)temp.Clone();
        }
        finally
        {
            DestroyIcon(handle);
        }
    }

    [DllImport("user32.dll")]
    private static extern void SendMessage(IntPtr hWnd, int msg, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll")]
    private static extern void DestroyIcon(IntPtr hIcon);
}
