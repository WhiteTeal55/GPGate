using System.Drawing.Drawing2D;

namespace GPGate;

// 自绘用的圆角矩形路径
internal static class Shapes
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
