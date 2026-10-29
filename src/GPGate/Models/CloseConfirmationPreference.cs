using Microsoft.Win32;

namespace GPGate;

internal static class CloseConfirmationPreference
{
    private const string SettingsKey = @"Software\GPGate";
    private const string ValueName = "SkipCloseConfirmation";

    internal static bool IsEnabled =>
        Registry.GetValue($@"HKEY_CURRENT_USER\{SettingsKey}", ValueName, 0) is not int value || value != 1;

    internal static void SetEnabled(bool enabled)
    {
        using RegistryKey settings = Registry.CurrentUser.CreateSubKey(SettingsKey);
        settings.SetValue(ValueName, enabled ? 0 : 1, RegistryValueKind.DWord);
    }
}
