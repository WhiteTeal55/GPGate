using System.Media;
using System.Runtime.InteropServices;

namespace GPGate;

internal static class WindowAttention
{
    private const uint FlashwTray = 0x00000002;
    private const uint FlashwTimerNoForeground = 0x0000000C;

    internal static void Notify(Form form, SystemSound sound)
    {
        sound.Play();

        Form target = form.ShowInTaskbar ? form : form.Owner ?? form;
        FlashWindowInfo info = new()
        {
            Size = (uint)Marshal.SizeOf<FlashWindowInfo>(),
            Handle = target.Handle,
            Flags = FlashwTray | FlashwTimerNoForeground,
        };
        _ = FlashWindowEx(ref info);
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct FlashWindowInfo
    {
        internal uint Size;
        internal IntPtr Handle;
        internal uint Flags;
        internal uint Count;
        internal uint Timeout;
    }

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool FlashWindowEx(ref FlashWindowInfo info);
}
