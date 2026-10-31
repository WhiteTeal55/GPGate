using System.Runtime.InteropServices;

namespace GPGate;

// 系统深浅色切换时刷新已打开的窗口
internal static class ThemeRefresh
{
    private const int WmSettingChange = 0x001A;
    private const int DwmwaUseImmersiveDarkMode = 20;

    // 收到深浅色变化就刷新并返回 true；applyTheme 是窗口自己的配色逻辑
    internal static bool TryApply(Form form, Message m, Action? applyTheme = null)
    {
        if (m.Msg != WmSettingChange || !IsImmersiveColorSet(m.LParam))
            return false;

        ThemePreference.ApplyColorMode();
        Apply(form, ThemePreference.IsDark, applyTheme);
        return true;
    }

    internal static void RefreshOpenForms()
    {
        bool dark = ThemePreference.IsDark;
        foreach (Form form in Application.OpenForms)
            Apply(form, dark, form is MainForm main ? main.ApplyTheme : null);
    }

    // 命令行路径没走过 GUI 初始化，补一次全局配色
    internal static void EnsureColorMode() => ThemePreference.ApplyColorMode();

    private static bool IsImmersiveColorSet(IntPtr lParam) =>
        Marshal.PtrToStringUni(lParam) is string name &&
        name.Equals("ImmersiveColorSet", StringComparison.OrdinalIgnoreCase);

    // 按钮的 UserPaint 不随主题重算（不重设就点不动），原生部分（复选框、滚动条）还要重设 uxtheme
    private static void Refresh(Control root, bool dark)
    {
        Palette palette = dark ? Palette.Dark : Palette.Light;

        foreach (Control child in root.Controls)
        {
            Refresh(child, dark);

            if (child is ButtonBase button and not (FlatButton or LinkButton))
            {
                FlatStyle style = button.FlatStyle;
                button.FlatStyle = style == FlatStyle.Standard ? FlatStyle.Popup : FlatStyle.Standard;
                button.FlatStyle = style;
                ApplyNativeTheme(button, dark);
            }

            switch (child)
            {
                case LinkLabel label:
                    // 前景色在构造时被主题写死过，得跟着换回来
                    label.ForeColor = palette.Text;
                    label.LinkColor = label.ActiveLinkColor = label.VisitedLinkColor = palette.ChipText;
                    break;
                case LinkButton linkButton:
                    linkButton.ForeColor = palette.ChipText;
                    break;
                case Panel { Height: 1 } separator:
                    separator.BackColor = palette.Separator;
                    break;
                case TextBox textBox:
                    // 颜色取调色板（SystemColors 切换瞬间可能是旧值）；重建句柄才能换掉原生背景和滚动条
                    textBox.BackColor = palette.FieldBackground;
                    textBox.ForeColor = palette.Text;
                    (textBox as DetailsBox)?.ReloadTheme(dark);
                    break;
            }
        }
    }

    private static void Apply(Form form, bool dark, Action? applyTheme)
    {
        Control? focused = form.ActiveControl;
        Refresh(form, dark);
        applyTheme?.Invoke();
        ApplyTitleBar(form, dark);
        form.ActiveControl = focused;
        form.Invalidate(true);
        form.Update();
    }

    private static void ApplyTitleBar(Form form, bool dark)
    {
        int value = dark ? 1 : 0;
        _ = DwmSetWindowAttribute(form.Handle, DwmwaUseImmersiveDarkMode, ref value, sizeof(int));
    }

    // 原生控件按当前深浅色重设 uxtheme，null 表示恢复默认（浅色）
    private static void ApplyNativeTheme(Control control, bool dark)
    {
        if (control.IsHandleCreated)
            _ = SetWindowTheme(control.Handle, dark ? "DarkMode_Explorer" : null, null);
    }

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int value, int size);

    [DllImport("uxtheme.dll", CharSet = CharSet.Unicode)]
    private static extern int SetWindowTheme(IntPtr hwnd, string? subApp, string? subId);
}
