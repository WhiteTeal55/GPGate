namespace GPGate;

internal static class Dialog
{
    internal static void Show(IWin32Window? owner, TaskDialogIcon icon, string text,
        string? heading = null, string? details = null)
    {
        Application.EnableVisualStyles();

        using DialogForm dialog = new(icon, text, heading, details);
        // 没有 owner（命令行/异常路径）时居中屏幕，有 owner 时居中于 owner
        dialog.StartPosition = owner is null ? FormStartPosition.CenterScreen : FormStartPosition.CenterParent;
        dialog.Shown += (_, _) =>
        {
            dialog.Activate();
            WindowFocus.BringToForeground(dialog.Handle);
        };
        _ = owner is null ? dialog.ShowDialog() : dialog.ShowDialog(owner);
    }
}
