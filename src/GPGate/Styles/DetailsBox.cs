namespace GPGate;

// 只读多行文本框：原生背景和滚动条缓存在句柄里，主题变了要重建句柄
internal sealed class DetailsBox : TextBox
{
    private bool _dark = Application.IsDarkModeEnabled; // 与建句柄时的主题一致

    // 同色广播不重建，省得滚动位置被重置
    internal void ReloadTheme(bool dark)
    {
        if (_dark == dark)
            return;

        _dark = dark;
        if (IsHandleCreated)
            RecreateHandle();
    }
}
