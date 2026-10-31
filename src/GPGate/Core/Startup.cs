using System.Globalization;
using Microsoft.Win32;

namespace GPGate;

// 开机自启：写 HKCU Run，程序会出现在「设置 > 应用 > 启动」里。
internal static class Startup
{
    private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ApprovedKey = @"Software\Microsoft\Windows\CurrentVersion\Explorer\StartupApproved\Run";
    private static readonly string ValueName = AppInfo.Name;

    // 写注册表失败时的提示，托盘菜单用
    internal const string ErrorMessage = "开机自启设置失败，可能需要管理员权限。";

    // 最近一次写入是否成功
    internal static bool CanManageStartup { get; private set; } = true;

    internal static bool IsEnabled => RunValue is not null && !IsDisabled;

    internal static bool Enable() => Guard(() =>
    {
        // 先清禁用标记，失败就不写 Run，避免半成功
        RemoveValue(ApprovedKey);
        WriteValue(Command());
    });

    // 各处入口都调用：系统里被关掉就清理，exe 挪过位置就重写路径
    internal static bool Sync() => Guard(() =>
    {
        if (IsDisabled)
        {
            DeleteValues();
            return;
        }

        if (RunValue is { } value)
        {
            string command = Command();
            if (value != command)
                WriteValue(command);
        }
    });

    internal static bool Disable() => Guard(DeleteValues);

    internal static bool Toggle() => IsEnabled ? Disable() : Enable();

    private static object? ReadValue(string key)
    {
        try
        {
            return Registry.GetValue($@"HKEY_CURRENT_USER\{key}", ValueName, null);
        }
        catch (Exception)
        {
            return null; // 策略/权限限制时当作未设置
        }
    }

    private static string? RunValue => ReadValue(RunKey) as string;

    // 首字节奇数=关闭（设置页 0x01，旧格式 0x03），偶数=开启
    private static bool IsDisabled =>
        ReadValue(ApprovedKey) is byte[] { Length: > 0 } state
        && (state[0] & 1) != 0;

    private static string Command() => $"\"{Environment.ProcessPath}\"";

    private static void WriteValue(string command)
    {
        using RegistryKey run = Registry.CurrentUser.CreateSubKey(RunKey);
        run.SetValue(ValueName, command);
    }

    private static void DeleteValues()
    {
        RemoveValue(RunKey);
        RemoveValue(ApprovedKey);
    }

    private static void RemoveValue(string key) =>
        Registry.CurrentUser.OpenSubKey(key, true)?.DeleteValue(ValueName, false);

    private static bool Guard(Action action)
    {
        try
        {
            action();
            CanManageStartup = true;
            return true;
        }
        catch (Exception ex)
        {
            CanManageStartup = false;
            _ = Log.Warn($"开机自启设置失败: {ex.Message}");
            return false;
        }
    }
}

internal static class StartupNotification
{
    private static readonly string SettingsKey = $@"Software\{AppInfo.Name}";
    private const string ValueName = "ShowStartupNotification";

    internal const string ErrorMessage = "启动提示设置失败，可能需要管理员权限。";

    internal static bool IsEnabled
    {
        get
        {
            try
            {
                object? value = Registry.GetValue($@"HKEY_CURRENT_USER\{SettingsKey}", ValueName, 1);
                return Convert.ToInt32(value ?? 1, CultureInfo.InvariantCulture) != 0;
            }
            catch (Exception ex)
            {
                _ = Log.Warn($"读取启动提示设置失败: {ex.Message}");
                return true;
            }
        }
    }

    internal static bool Toggle()
    {
        try
        {
            using RegistryKey settings = Registry.CurrentUser.CreateSubKey(SettingsKey);
            settings.SetValue(ValueName, IsEnabled ? 0 : 1, RegistryValueKind.DWord);
            return true;
        }
        catch (Exception ex)
        {
            _ = Log.Warn($"启动提示设置失败: {ex.Message}");
            return false;
        }
    }
}
