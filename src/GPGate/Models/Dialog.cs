namespace GPGate;

internal static class Dialog
{
    internal static void Show(IWin32Window? owner, TaskDialogIcon icon, string text,
        string? heading = null, string? details = null)
    {
        using DialogForm dialog = new(icon, text, heading, details);
        // 没有 owner 时居中屏幕并给个任务栏按钮，有 owner 时居中于 owner
        dialog.StartPosition = owner is null ? FormStartPosition.CenterScreen : FormStartPosition.CenterParent;
        dialog.ShowInTaskbar = owner is null;
        dialog.Shown += (_, _) =>
        {
            dialog.Activate();
            WindowFocus.BringToForeground(dialog.Handle);
        };
        _ = owner is null ? dialog.ShowDialog() : dialog.ShowDialog(owner);
    }
}
