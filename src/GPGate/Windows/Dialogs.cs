namespace GPGate;

// 统一的新版对话框，错误/提示都走这里
internal static class Dialogs
{
    internal static void Show(IWin32Window? owner, TaskDialogIcon icon, string text,
        string? heading = null, string? expander = null)
    {
        // 无控制台的早启动路径还没初始化过视觉样式
        Application.EnableVisualStyles();

        TaskDialogPage page = new()
        {
            Caption = AppInfo.Name,
            Icon = icon,
            Heading = heading,
            Text = text,
            Expander = expander is null ? null : new TaskDialogExpander(expander),
            Buttons = { TaskDialogButton.OK },
        };

        _ = owner is null ? TaskDialog.ShowDialog(page) : TaskDialog.ShowDialog(owner, page);
    }
}
