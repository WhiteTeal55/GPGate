using System.Reflection;

namespace GPGate;

// 程序集元数据集中入口，避免各处写死名称、版本、版权
internal static class AppInfo
{
    private static readonly Assembly Self = Assembly.GetExecutingAssembly();

    internal static string Name { get; } = Self.GetName().Name!;
    internal static string Version { get; } = Application.ProductVersion.Split('+')[0];
    internal static string Product { get; } = Application.ProductName!;
    internal static string Copyright { get; } = Self.GetCustomAttribute<AssemblyCopyrightAttribute>()!.Copyright;
}
