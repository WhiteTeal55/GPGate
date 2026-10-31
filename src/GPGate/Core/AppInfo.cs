using System.Reflection;

namespace GPGate;

internal static class AppInfo
{
    // 图标嵌入资源名，与 csproj 的 LogicalName 对应
    internal const string IconResource = "GPGate.app.ico";
    internal const string OffIconResource = "GPGate.app-off.ico";
    internal const string OffLightIconResource = "GPGate.app-off-light.ico";

    private static readonly Assembly Self = Assembly.GetExecutingAssembly();

    internal static string Name { get; } = Self.GetName().Name!;
    internal static string Version { get; } = Application.ProductVersion.Split('+')[0];
    internal static string Product { get; } = Application.ProductName!;
    internal static string Copyright { get; } = Self.GetCustomAttribute<AssemblyCopyrightAttribute>()!.Copyright;

    // 加载应用图标，调用方负责释放
    internal static Icon LoadAppIcon()
    {
        using Stream stream = Self.GetManifestResourceStream(IconResource)
            ?? throw new InvalidOperationException($"缺少图标资源 {IconResource}");
        return new Icon(stream, new Size(256, 256));
    }
}
