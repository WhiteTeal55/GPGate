using System.Drawing.Drawing2D;

namespace GPGate;

internal static class Ui
{
    public static GraphicsPath RoundRect(Rectangle rect, int radius)
    {
        GraphicsPath path = new();
        if (radius <= 0)
        {
            path.AddRectangle(rect);
            return path;
        }

        int d = radius * 2;
        path.AddArc(rect.X, rect.Y, d, d, 180, 90);
        path.AddArc(rect.Right - d, rect.Y, d, d, 270, 90);
        path.AddArc(rect.Right - d, rect.Bottom - d, d, d, 0, 90);
        path.AddArc(rect.X, rect.Bottom - d, d, d, 90, 90);
        path.CloseFigure();
        return path;
    }
}

// 深浅两套配色，强调色取自图标的品牌蓝
internal sealed class Palette
{
    public static readonly Color Accent = Color.FromArgb(0, 147, 221);
    public static readonly Color AccentHover = Color.FromArgb(0, 165, 240);
    public static readonly Color AccentPress = Color.FromArgb(0, 124, 188);
    public static readonly Color Danger = Color.FromArgb(226, 74, 79);
    public static readonly Color DangerHover = Color.FromArgb(238, 92, 97);
    public static readonly Color DangerPress = Color.FromArgb(200, 58, 63);
    public static readonly Color CloseHover = Color.FromArgb(196, 43, 28);
    public static readonly Color ClosePress = Color.FromArgb(158, 31, 20);

    public static readonly Palette Dark = new()
    {
        Background = Color.FromArgb(26, 27, 31),
        Border = Color.FromArgb(48, 50, 57),
        FieldBorder = Color.FromArgb(74, 77, 85),
        FieldBackground = Color.FromArgb(50, 50, 50),
        Separator = Color.FromArgb(74, 74, 74),
        Text = Color.FromArgb(243, 244, 246),
        Muted = Color.FromArgb(138, 143, 152),
        GhostFill = Color.FromArgb(40, 42, 48),
        GhostFillHover = Color.FromArgb(53, 56, 63),
        ChipBackground = Color.FromArgb(20, 46, 64),
        ChipText = Color.FromArgb(130, 208, 246),
        ChipDot = Color.FromArgb(0, 163, 240),
        StoppedChipBackground = Color.FromArgb(40, 42, 48),
        StoppedChipText = Color.FromArgb(150, 155, 164),
        StoppedChipDot = Color.FromArgb(112, 117, 126),
    };

    public static readonly Palette Light = new()
    {
        Background = Color.White,
        Border = Color.FromArgb(232, 234, 238),
        FieldBorder = Color.FromArgb(108, 114, 124),
        FieldBackground = Color.White,
        Separator = Color.FromArgb(160, 160, 160),
        Text = Color.FromArgb(22, 24, 29),
        Muted = Color.FromArgb(108, 114, 124),
        GhostFill = Color.FromArgb(243, 244, 247),
        GhostFillHover = Color.FromArgb(232, 234, 239),
        ChipBackground = Color.FromArgb(229, 244, 253),
        ChipText = Color.FromArgb(0, 106, 165),
        ChipDot = Color.FromArgb(0, 147, 221),
        StoppedChipBackground = Color.FromArgb(243, 244, 247),
        StoppedChipText = Color.FromArgb(108, 114, 124),
        StoppedChipDot = Color.FromArgb(163, 168, 177),
    };

    public Color Background { get; init; }
    public Color Border { get; init; }
    public Color FieldBorder { get; init; }
    public Color FieldBackground { get; init; }
    public Color Separator { get; init; }
    public Color Text { get; init; }
    public Color Muted { get; init; }
    public Color GhostFill { get; init; }
    public Color GhostFillHover { get; init; }
    public Color ChipBackground { get; init; }
    public Color ChipText { get; init; }
    public Color ChipDot { get; init; }
    public Color StoppedChipBackground { get; init; }
    public Color StoppedChipText { get; init; }
    public Color StoppedChipDot { get; init; }
}
