using System;
using System.Drawing;
using System.Drawing.Drawing2D;

namespace TcpTool.App
{
    /// <summary>统一的配色、字体与圆角绘制工具。所有界面色值都从这里取。</summary>
    public static class Theme
    {
        // 背景
        public static readonly Color WindowBack = Color.FromArgb(241, 245, 249);   // 页面底色（浅冷灰）
        public static readonly Color CardBack = Color.White;                       // 卡片
        public static readonly Color CardBorder = Color.FromArgb(226, 232, 240);   // 卡片描边
        public static readonly Color HeaderBack = Color.FromArgb(15, 23, 42);      // 顶部深色栏
        public static readonly Color HeaderBorder = Color.FromArgb(30, 41, 59);

        // 文字
        public static readonly Color TextPrimary = Color.FromArgb(15, 23, 42);
        public static readonly Color TextSecondary = Color.FromArgb(100, 116, 139);
        public static readonly Color TextMuted = Color.FromArgb(148, 163, 184);     // 只用于装饰，不用于正文
        public static readonly Color TextOnDark = Color.FromArgb(226, 232, 240);

        // 强调色
        public static readonly Color Accent = Color.FromArgb(37, 99, 235);
        public static readonly Color AccentHover = Color.FromArgb(29, 78, 216);
        public static readonly Color AccentPress = Color.FromArgb(30, 64, 175);
        public static readonly Color AccentTint = Color.FromArgb(239, 244, 255);
        public static readonly Color Success = Color.FromArgb(16, 185, 129);
        public static readonly Color Warning = Color.FromArgb(245, 158, 11);
        public static readonly Color Danger = Color.FromArgb(220, 38, 38);
        public static readonly Color DangerHover = Color.FromArgb(185, 28, 28);
        public static readonly Color GhostHover = Color.FromArgb(241, 245, 249);

        // 输入与分段控件
        public static readonly Color InputBack = Color.White;
        public static readonly Color InputBorder = Color.FromArgb(203, 213, 225);
        public static readonly Color Track = Color.FromArgb(237, 241, 247);

        // 日志区
        public static readonly Color LogBack = Color.FromArgb(251, 252, 254);
        public static readonly Color LogReceived = Color.FromArgb(30, 41, 59);
        public static readonly Color LogSent = Color.FromArgb(29, 78, 216);
        public static readonly Color LogTime = Color.FromArgb(100, 116, 139);
        public static readonly Color LogWho = Color.FromArgb(15, 118, 110);

        private static Font _title;
        private static Font _section;
        private static Font _body;
        private static Font _small;
        private static Font _mono;

        public static Font TitleFont
        {
            get
            {
                if (_title == null) _title = new Font("Microsoft YaHei UI", 13F, FontStyle.Bold);
                return _title;
            }
        }

        public static Font SectionFont
        {
            get
            {
                if (_section == null) _section = new Font("Microsoft YaHei UI", 9.75F, FontStyle.Bold);
                return _section;
            }
        }

        public static Font BodyFont
        {
            get
            {
                if (_body == null) _body = new Font("Microsoft YaHei UI", 9F);
                return _body;
            }
        }

        public static Font SmallFont
        {
            get
            {
                if (_small == null) _small = new Font("Microsoft YaHei UI", 8.25F);
                return _small;
            }
        }

        public static Font MonoFont
        {
            get
            {
                if (_mono == null) _mono = new Font("Consolas", 10F);
                return _mono;
            }
        }

        /// <summary>四角同半径的圆角矩形路径。</summary>
        public static GraphicsPath RoundedRect(Rectangle bounds, int radius)
        {
            return RoundedRect(bounds, radius, radius, radius, radius);
        }

        /// <summary>四角可分别指定半径的圆角矩形路径（0 表示直角）。</summary>
        public static GraphicsPath RoundedRect(Rectangle bounds, int topLeft, int topRight, int bottomRight, int bottomLeft)
        {
            GraphicsPath path = new GraphicsPath();
            if (bounds.Width <= 0 || bounds.Height <= 0)
            {
                path.AddRectangle(new Rectangle(bounds.X, bounds.Y, Math.Max(1, bounds.Width), Math.Max(1, bounds.Height)));
                return path;
            }

            int limit = Math.Min(bounds.Width, bounds.Height) / 2;
            topLeft = Math.Max(0, Math.Min(topLeft, limit));
            topRight = Math.Max(0, Math.Min(topRight, limit));
            bottomRight = Math.Max(0, Math.Min(bottomRight, limit));
            bottomLeft = Math.Max(0, Math.Min(bottomLeft, limit));

            if (topLeft > 0)
                path.AddArc(bounds.X, bounds.Y, topLeft * 2, topLeft * 2, 180, 90);
            else
                path.AddLine(bounds.X, bounds.Y, bounds.X, bounds.Y);

            if (topRight > 0)
                path.AddArc(bounds.Right - topRight * 2, bounds.Y, topRight * 2, topRight * 2, 270, 90);
            else
                path.AddLine(bounds.Right, bounds.Y, bounds.Right, bounds.Y);

            if (bottomRight > 0)
                path.AddArc(bounds.Right - bottomRight * 2, bounds.Bottom - bottomRight * 2, bottomRight * 2, bottomRight * 2, 0, 90);
            else
                path.AddLine(bounds.Right, bounds.Bottom, bounds.Right, bounds.Bottom);

            if (bottomLeft > 0)
                path.AddArc(bounds.X, bounds.Bottom - bottomLeft * 2, bottomLeft * 2, bottomLeft * 2, 90, 90);
            else
                path.AddLine(bounds.X, bounds.Bottom, bounds.X, bounds.Bottom);

            path.CloseFigure();
            return path;
        }

        /// <summary>按比例混合两个颜色（ratio=0 取 a，=1 取 b）。</summary>
        public static Color Blend(Color a, Color b, double ratio)
        {
            ratio = Math.Max(0.0, Math.Min(1.0, ratio));
            return Color.FromArgb(
                (int)Math.Round(a.R + (b.R - a.R) * ratio),
                (int)Math.Round(a.G + (b.G - a.G) * ratio),
                (int)Math.Round(a.B + (b.B - a.B) * ratio));
        }

        /// <summary>WCAG 相对亮度，用于对比度自检。</summary>
        public static double RelativeLuminance(Color color)
        {
            double r = Channel(color.R);
            double g = Channel(color.G);
            double b = Channel(color.B);
            return 0.2126 * r + 0.7152 * g + 0.0722 * b;
        }

        /// <summary>WCAG 对比度（1~21）。</summary>
        public static double ContrastRatio(Color foreground, Color background)
        {
            double l1 = RelativeLuminance(foreground);
            double l2 = RelativeLuminance(background);
            if (l2 > l1)
            {
                double swap = l1;
                l1 = l2;
                l2 = swap;
            }
            return (l1 + 0.05) / (l2 + 0.05);
        }

        private static double Channel(int value)
        {
            double v = value / 255.0;
            return v <= 0.03928 ? v / 12.92 : Math.Pow((v + 0.055) / 1.055, 2.4);
        }
    }
}
