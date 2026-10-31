using System.Globalization;
using Microsoft.Win32;

namespace GPGate;

internal enum ThemeMode
{
    System,
    Light,
    Dark,
}

internal static class ThemePreference
{
    private static readonly string SettingsKey = $@"Software\{AppInfo.Name}";
    private const string ValueName = "ThemeMode";
    private const ThemeMode DefaultMode = ThemeMode.System;

    private static ThemeMode _mode = ReadMode();

    internal const string ErrorMessage = "主题设置保存失败，可能需要管理员权限。";

    internal static ThemeMode Mode => _mode;

    internal static bool IsDark => _mode switch
    {
        ThemeMode.Dark => true,
        ThemeMode.Light => false,
        _ => Application.IsDarkModeEnabled,
    };

    internal static void ApplyColorMode() =>
        Application.SetColorMode(_mode switch
        {
            ThemeMode.Dark => SystemColorMode.Dark,
            ThemeMode.Light => SystemColorMode.Classic,
            _ => SystemColorMode.System,
        });

    internal static bool TrySetMode(ThemeMode mode)
    {
        try
        {
            using RegistryKey settings = Registry.CurrentUser.CreateSubKey(SettingsKey);
            settings.SetValue(ValueName, (int)mode, RegistryValueKind.DWord);
        }
        catch (Exception ex)
        {
            _ = Log.Warn($"主题设置失败: {ex.Message}");
            return false;
        }

        _mode = mode;
        ApplyColorMode();
        ThemeRefresh.RefreshOpenForms();
        return true;
    }

    private static ThemeMode ReadMode()
    {
        try
        {
            object? value = Registry.GetValue($@"HKEY_CURRENT_USER\{SettingsKey}", ValueName, (int)DefaultMode);
            int mode = Convert.ToInt32(value ?? 0, CultureInfo.InvariantCulture);
            return Enum.IsDefined((ThemeMode)mode) ? (ThemeMode)mode : DefaultMode;
        }
        catch (Exception ex)
        {
            _ = Log.Warn($"读取主题设置失败: {ex.Message}");
            return DefaultMode;
        }
    }
}
