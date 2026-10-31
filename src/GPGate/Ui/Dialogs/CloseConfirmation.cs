using System.Globalization;
using Microsoft.Win32;

namespace GPGate;

internal static class CloseConfirmation
{
    private static readonly string SettingsKey = $@"Software\{AppInfo.Name}";
    private const string ValueName = "SkipCloseConfirmation";

    internal const string ErrorMessage = "退出确认设置失败，请检查注册表写入权限。";

    // 读失败按未设置处理
    private static bool? ReadValue()
    {
        try
        {
            object? value = Registry.GetValue($@"HKEY_CURRENT_USER\{SettingsKey}", ValueName, 0);
            return Convert.ToInt64(value, CultureInfo.InvariantCulture) != 1;
        }
        catch (Exception)
        {
            return null;
        }
    }

    internal static bool IsEnabled => ReadValue() is not false;

    // 返回 true 表示继续关闭
    internal static bool ConfirmClose(IWin32Window? owner)
    {
        if (!IsEnabled)
            return true;

        DialogOutcome outcome = Dialog.Show(owner, TaskDialogIcon.Information,
            string.Empty, "是否退出此软件？", checkBoxText: "不再提示", showCancel: true);
        if (!outcome.Confirmed)
            return false;

        // 存不下设置就不关闭
        if (!outcome.Checked || WriteValue(false))
            return true;

        Dialog.Show(owner, TaskDialogIcon.Error, ErrorMessage, "保存设置失败");
        return false;
    }

    internal static bool Toggle() => WriteValue(!IsEnabled);

    internal static bool DeleteSettings() => Guard(() =>
    {
        Registry.CurrentUser.DeleteSubKeyTree(SettingsKey, false);
    });

    internal static bool WriteValue(bool enabled) => Guard(() =>
    {
        using RegistryKey settings = Registry.CurrentUser.CreateSubKey(SettingsKey);
        settings.SetValue(ValueName, enabled ? 0 : 1, RegistryValueKind.DWord);
    });

    private static bool Guard(Action action)
    {
        try
        {
            action();
            return true;
        }
        catch (Exception ex)
        {
            _ = Log.Warn($"退出确认设置失败: {ex.Message}");
            return false;
        }
    }
}
