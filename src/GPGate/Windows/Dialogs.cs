namespace GPGate;

internal static class Dialogs
{
    internal static void Show(IWin32Window? owner, TaskDialogIcon icon, string text,
        string? heading = null, string? expander = null)
    {
        Application.EnableVisualStyles();
        Application.SetColorMode(SystemColorMode.System);

        using MessageDialogForm dialog = new(icon, text, heading, expander);
        _ = owner is null ? dialog.ShowDialog() : dialog.ShowDialog(owner);
    }
}
