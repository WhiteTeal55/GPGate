using System.Globalization;
using Microsoft.Win32;

namespace GPGate;

// 退出确认：写注册表 + 关闭前确认窗口
internal static class CloseConfirmation
{
    private static readonly string SettingsKey = $@"Software\{AppInfo.Name}";
    private const string ValueName = "SkipCloseConfirmation";

    // 写失败时的提示，托盘和关闭确认共用
    internal const string ErrorMessage = "退出确认设置失败，可能需要管理员权限。";

    // 读失败按未设置处理
    private static bool? ReadValue()
    {
        try
        {
            object? value = Registry.GetValue($@"HKEY_CURRENT_USER\{SettingsKey}", ValueName, 0);
            return Convert.ToInt64(value ?? 0, CultureInfo.InvariantCulture) != 1;
        }
        catch (Exception)
        {
            return null;
        }
    }

    internal static bool IsEnabled => ReadValue() is true;

    // 返回 true 表示继续关闭
    internal static bool ConfirmClose(IWin32Window? owner)
    {
        if (!IsEnabled)
            return true;

        using CloseConfirmationForm form = new();

        // 主窗藏在托盘时没有可见 owner，按屏幕居中
        form.StartPosition = owner is null ? FormStartPosition.CenterScreen : FormStartPosition.CenterParent;
        DialogResult result = owner is null ? form.ShowDialog() : form.ShowDialog(owner);
        if (result != DialogResult.Yes)
            return false;

        // 存不下设置就不关闭
        if (!form.DontAskAgain || WriteValue(false))
            return true;

        Dialog.Show(owner, TaskDialogIcon.Error, ErrorMessage, "保存设置失败");
        return false;
    }

    internal static bool Toggle() => WriteValue(!IsEnabled);

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
