using System.Runtime.InteropServices;

namespace GPGate;

internal sealed class Palette
{
    public static readonly Palette Dark = new()
    {
        FieldBorder = Color.FromArgb(74, 77, 85),
        FieldBackground = Color.FromArgb(50, 50, 50),
        Separator = Color.FromArgb(74, 74, 74),
        Text = Color.FromArgb(243, 244, 246),
        ChipText = Color.FromArgb(130, 208, 246),
    };

    public static readonly Palette Light = new()
    {
        FieldBorder = Color.FromArgb(108, 114, 124),
        FieldBackground = Color.White,
        Separator = Color.FromArgb(160, 160, 160),
        Text = Color.FromArgb(22, 24, 29),
        ChipText = Color.FromArgb(0, 106, 165),
    };

    public Color FieldBorder { get; init; }
    public Color FieldBackground { get; init; }
    public Color Separator { get; init; }
    public Color Text { get; init; }
    public Color ChipText { get; init; }
}

internal static class ThemeRefresh
{
    private const int WmSettingChange = 0x001A;
    private const int DwmwaUseImmersiveDarkMode = 20;

    private static bool? _appliedDark;

    // 收到深浅色变化就刷新并返回 true
    internal static bool TryApply(Form form, Message m)
    {
        if (!IsThemeChange(m))
            return false;

        bool dark = Application.IsDarkModeEnabled;
        Control? focused = form.ActiveControl;

        ApplyColorMode(dark);
        Refresh(form, dark);
        ApplyTitleBar(form, dark);

        form.ActiveControl = focused;
        form.Invalidate(true);
        form.Update();
        return true;
    }

    // 无窗口宿主（托盘）只切全局配色
    internal static bool TryApply(Message m)
    {
        if (!IsThemeChange(m))
            return false;

        ApplyColorMode(Application.IsDarkModeEnabled);
        return true;
    }

    // 命令行路径没走过 GUI 初始化，补一次全局配色
    internal static void EnsureColorMode()
    {
        if (Application.ColorMode != SystemColorMode.System)
            Application.SetColorMode(SystemColorMode.System);
    }

    private static bool IsThemeChange(Message m) =>
        m.Msg == WmSettingChange
        && Marshal.PtrToStringUni(m.LParam) is string name
        && name.Equals("ImmersiveColorSet", StringComparison.OrdinalIgnoreCase);

    // 多个窗口都会收到广播，全局配色只切一次
    private static void ApplyColorMode(bool dark)
    {
        if (_appliedDark == dark)
            return;

        _appliedDark = dark;
        Application.SetColorMode(SystemColorMode.System);
    }

    // 重设 FlatStyle 让 UserPaint 重算，原生控件还要重设 uxtheme
    private static void Refresh(Control root, bool dark)
    {
        Palette palette = dark ? Palette.Dark : Palette.Light;

        foreach (Control child in root.Controls)
        {
            Refresh(child, dark);

            if (child is ButtonBase button)
            {
                FlatStyle style = button.FlatStyle;
                button.FlatStyle = style == FlatStyle.Standard ? FlatStyle.Popup : FlatStyle.Standard;
                button.FlatStyle = style;
                ApplyNativeTheme(button, dark);
            }

            switch (child)
            {
                case LinkLabel linkLabel:
                    linkLabel.ForeColor = palette.Text;
                    linkLabel.LinkColor = palette.ChipText;
                    linkLabel.ActiveLinkColor = palette.ChipText;
                    linkLabel.VisitedLinkColor = palette.ChipText;
                    break;
                case Panel { Height: 1 } separator:
                    separator.BackColor = palette.Separator;
                    break;
                case DetailsBox details:
                    details.BackColor = palette.FieldBackground;
                    details.ForeColor = palette.Text;
                    details.ReloadTheme(dark);
                    break;
            }
        }
    }

    private static void ApplyTitleBar(Form form, bool dark)
    {
        int value = dark ? 1 : 0;
        _ = DwmSetWindowAttribute(form.Handle, DwmwaUseImmersiveDarkMode, ref value, sizeof(int));
    }

    // null 恢复浅色主题
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
