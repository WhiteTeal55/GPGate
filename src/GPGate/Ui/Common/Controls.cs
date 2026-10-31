namespace GPGate;

// 只读多行文本框：主题变化时重建句柄，才能换掉原生背景和滚动条
internal sealed class DetailsBox : TextBox
{
    private bool _dark = Application.IsDarkModeEnabled;

    // 同色不重建，避免滚动位置重置
    internal void ReloadTheme(bool dark)
    {
        if (_dark == dark)
            return;

        _dark = dark;
        if (IsHandleCreated)
            RecreateHandle();
    }
}
