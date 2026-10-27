using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace TcpTool.App
{
    /// <summary>白色圆角卡片，标题画在卡片内部左上角，左侧带一条强调色竖条。</summary>
    public class CardPanel : Panel
    {
        private string _title = string.Empty;

        public int Radius = 10;

        public string Title
        {
            get { return _title; }
            set
            {
                _title = value ?? string.Empty;
                Invalidate();
            }
        }

        public CardPanel()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            BackColor = Theme.WindowBack;
            Font = Theme.BodyFont;
            Padding = new Padding(14, 46, 14, 14);
        }

        protected override void OnPaintBackground(PaintEventArgs e)
        {
            e.Graphics.Clear(Theme.WindowBack);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            Rectangle bounds = new Rectangle(0, 0, Width - 1, Height - 1);
            using (GraphicsPath path = Theme.RoundedRect(bounds, Radius))
            {
                using (SolidBrush fill = new SolidBrush(Theme.CardBack))
                {
                    g.FillPath(fill, path);
                }
                using (Pen border = new Pen(Theme.CardBorder))
                {
                    g.DrawPath(border, path);
                }
            }

            if (_title.Length > 0)
            {
                using (SolidBrush bar = new SolidBrush(Theme.Accent))
                {
                    g.FillRectangle(bar, 14, 19, 3, 13);
                }
                TextRenderer.DrawText(g, _title, Theme.SectionFont, new Point(24, 16), Theme.TextPrimary,
                    TextFormatFlags.Left | TextFormatFlags.NoPadding);
            }
        }
    }

    /// <summary>顶部深色标题栏。</summary>
    public class HeaderBar : Panel
    {
        public HeaderBar()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            BackColor = Theme.HeaderBack;
        }

        protected override void OnPaintBackground(PaintEventArgs e)
        {
            e.Graphics.Clear(Theme.HeaderBack);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            using (SolidBrush line = new SolidBrush(Theme.HeaderBorder))
            {
                e.Graphics.FillRectangle(line, 0, Height - 1, Width, 1);
            }
        }
    }

    /// <summary>带圆角与描边的容器（输入框外框、日志区外框）。</summary>
    public class Frame : Panel
    {
        public int Radius = 8;
        public Color FillColor = Theme.InputBack;
        public Color BorderColor = Theme.InputBorder;

        public Frame()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            BackColor = Theme.CardBack;
        }

        protected override void OnPaintBackground(PaintEventArgs e)
        {
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            e.Graphics.Clear(Theme.CardBack);

            Rectangle bounds = new Rectangle(0, 0, Width - 1, Height - 1);
            using (GraphicsPath path = Theme.RoundedRect(bounds, Radius))
            using (SolidBrush fill = new SolidBrush(FillColor))
            {
                e.Graphics.FillPath(fill, path);
            }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            Rectangle bounds = new Rectangle(0, 0, Width - 1, Height - 1);
            using (GraphicsPath path = Theme.RoundedRect(bounds, Radius))
            using (Pen pen = new Pen(BorderColor))
            {
                e.Graphics.DrawPath(pen, path);
            }
        }
    }

    /// <summary>单行/多行输入框：圆角边框 + 内部无边框 TextBox，获得焦点时边框变强调色。</summary>
    public class FlatInput : Frame
    {
        public TextBox Inner { get; private set; }

        public FlatInput(bool multiline)
        {
            Radius = 8;
            FillColor = Theme.InputBack;
            BorderColor = Theme.InputBorder;
            Padding = multiline ? new Padding(10, 8, 8, 8) : new Padding(10, 6, 8, 6);

            Inner = new TextBox();
            Inner.BorderStyle = BorderStyle.None;
            Inner.BackColor = Theme.InputBack;
            Inner.ForeColor = Theme.TextPrimary;
            Inner.Font = multiline ? Theme.MonoFont : Theme.BodyFont;
            Inner.Multiline = multiline;
            Inner.Dock = DockStyle.Fill;
            if (multiline)
            {
                Inner.ScrollBars = ScrollBars.Vertical;
                Inner.AcceptsReturn = true;
                Inner.WordWrap = false;
            }

            Inner.GotFocus += delegate
            {
                BorderColor = Theme.Accent;
                Invalidate();
            };
            Inner.LostFocus += delegate
            {
                BorderColor = Theme.InputBorder;
                Invalidate();
            };

            Controls.Add(Inner);
        }
    }

    /// <summary>扁平按钮：实心强调色 / 危险色 / 描边幽灵三种样式，带悬停与按下反馈。</summary>
    public class FlatButton : Button
    {
        public enum Kind
        {
            Primary,
            Danger,
            Ghost
        }

        private bool _hover;
        private bool _pressed;
        private Kind _buttonKind = Kind.Primary;

        public int Radius = 8;

        public Kind ButtonKind
        {
            get { return _buttonKind; }
            set
            {
                _buttonKind = value;
                Invalidate();
            }
        }

        public FlatButton()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            FlatStyle = FlatStyle.Flat;
            FlatAppearance.BorderSize = 0;
            Font = Theme.BodyFont;
            BackColor = Theme.CardBack;
            Cursor = Cursors.Hand;
        }

        protected override void OnMouseEnter(EventArgs e)
        {
            _hover = true;
            Invalidate();
            base.OnMouseEnter(e);
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            _hover = false;
            _pressed = false;
            Invalidate();
            base.OnMouseLeave(e);
        }

        protected override void OnMouseDown(MouseEventArgs mevent)
        {
            _pressed = true;
            Invalidate();
            base.OnMouseDown(mevent);
        }

        protected override void OnMouseUp(MouseEventArgs mevent)
        {
            _pressed = false;
            Invalidate();
            base.OnMouseUp(mevent);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            Color fill;
            Color textColor;
            Color border = Color.Empty;

            switch (_buttonKind)
            {
                case Kind.Danger:
                    fill = _pressed ? Theme.Blend(Theme.DangerHover, Color.Black, 0.15) : (_hover ? Theme.DangerHover : Theme.Danger);
                    textColor = Color.White;
                    break;
                case Kind.Ghost:
                    fill = _pressed ? Theme.Blend(Theme.GhostHover, Theme.TextPrimary, 0.06) : (_hover ? Theme.GhostHover : Theme.CardBack);
                    textColor = Theme.TextPrimary;
                    border = _hover ? Theme.InputBorder : Theme.CardBorder;
                    break;
                default:
                    fill = _pressed ? Theme.AccentPress : (_hover ? Theme.AccentHover : Theme.Accent);
                    textColor = Color.White;
                    break;
            }

            if (!Enabled)
            {
                fill = Theme.Blend(fill, Theme.CardBack, 0.65);
                textColor = Theme.TextMuted;
                border = Theme.CardBorder;
            }

            Rectangle bounds = new Rectangle(0, 0, Width - 1, Height - 1);
            using (GraphicsPath path = Theme.RoundedRect(bounds, Radius))
            {
                using (SolidBrush brush = new SolidBrush(fill))
                {
                    g.FillPath(brush, path);
                }
                if (border != Color.Empty)
                {
                    using (Pen pen = new Pen(border))
                    {
                        g.DrawPath(pen, path);
                    }
                }
            }

            TextRenderer.DrawText(g, Text, Font, bounds, textColor,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
        }
    }

    /// <summary>扁平复选框：16px 圆角方框，选中填充强调色并画对勾。</summary>
    public class FlatCheck : CheckBox
    {
        private bool _hover;

        public FlatCheck()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            Font = Theme.BodyFont;
            BackColor = Theme.CardBack;
            ForeColor = Theme.TextPrimary;
            AutoSize = false;
            Height = 22;
            Cursor = Cursors.Hand;
        }

        protected override void OnMouseEnter(EventArgs e)
        {
            _hover = true;
            Invalidate();
            base.OnMouseEnter(e);
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            _hover = false;
            Invalidate();
            base.OnMouseLeave(e);
        }

        protected override void OnCheckedChanged(EventArgs e)
        {
            Invalidate();
            base.OnCheckedChanged(e);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.Clear(BackColor);

            int boxSize = 16;
            int top = (Height - boxSize) / 2;
            Rectangle box = new Rectangle(0, top, boxSize, boxSize);

            using (GraphicsPath path = Theme.RoundedRect(box, 4))
            {
                if (Checked)
                {
                    Color fill = !Enabled
                        ? Theme.Blend(Theme.Accent, Theme.CardBack, 0.6)
                        : (_hover ? Theme.AccentHover : Theme.Accent);
                    using (SolidBrush brush = new SolidBrush(fill))
                    {
                        g.FillPath(brush, path);
                    }
                }
                else
                {
                    using (SolidBrush brush = new SolidBrush(Theme.InputBack))
                    {
                        g.FillPath(brush, path);
                    }
                    Color borderColor = !Enabled ? Theme.CardBorder : (_hover ? Theme.TextMuted : Theme.InputBorder);
                    using (Pen pen = new Pen(borderColor, 1.4f))
                    {
                        g.DrawPath(pen, path);
                    }
                }
            }

            if (Checked)
            {
                using (Pen pen = new Pen(Color.White, 1.8f))
                {
                    pen.StartCap = LineCap.Round;
                    pen.EndCap = LineCap.Round;
                    g.DrawLines(pen, new Point[]
                    {
                        new Point(box.Left + 4, box.Top + 8),
                        new Point(box.Left + 7, box.Top + 11),
                        new Point(box.Left + 12, box.Top + 5)
                    });
                }
            }

            Color textColor = Enabled ? ForeColor : Theme.TextMuted;
            TextRenderer.DrawText(g, Text, Font,
                new Rectangle(boxSize + 8, 0, Width - boxSize - 8, Height), textColor,
                TextFormatFlags.VerticalCenter | TextFormatFlags.Left | TextFormatFlags.EndEllipsis);
        }
    }

    /// <summary>分段选择器底座，负责画浅灰轨道。</summary>
    public class SegmentGroup : Panel
    {
        public SegmentGroup()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            BackColor = Theme.CardBack;
            Font = Theme.BodyFont;
        }

        protected override void OnPaintBackground(PaintEventArgs e)
        {
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            e.Graphics.Clear(Theme.CardBack);

            Rectangle bounds = new Rectangle(0, 0, Width - 1, Height - 1);
            using (GraphicsPath path = Theme.RoundedRect(bounds, 9))
            using (SolidBrush fill = new SolidBrush(Theme.Track))
            {
                e.Graphics.FillPath(fill, path);
            }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            Rectangle bounds = new Rectangle(0, 0, Width - 1, Height - 1);
            using (GraphicsPath path = Theme.RoundedRect(bounds, 9))
            using (Pen pen = new Pen(Theme.CardBorder))
            {
                e.Graphics.DrawPath(pen, path);
            }
        }
    }

    /// <summary>分段选择器里的一个分段（本质是 RadioButton，保留无障碍与互斥行为）。</summary>
    public class SegmentRadio : RadioButton
    {
        private bool _hover;
        private Font _boldFont;

        public bool RoundLeft = true;
        public bool RoundRight = true;

        public SegmentRadio()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            Font = Theme.BodyFont;
            BackColor = Theme.Track;
            ForeColor = Theme.TextPrimary;
            Cursor = Cursors.Hand;
            Height = 34;
        }

        protected override void OnMouseEnter(EventArgs e)
        {
            _hover = true;
            Invalidate();
            base.OnMouseEnter(e);
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            _hover = false;
            Invalidate();
            base.OnMouseLeave(e);
        }

        protected override void OnCheckedChanged(EventArgs e)
        {
            Invalidate();
            base.OnCheckedChanged(e);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.Clear(Theme.Track);

            Rectangle area = new Rectangle(0, 0, Width, Height);

            if (Checked)
            {
                Rectangle pill = new Rectangle(area.X, area.Y, area.Width - 1, area.Height - 1);
                using (GraphicsPath path = Theme.RoundedRect(pill, 6))
                {
                    using (SolidBrush brush = new SolidBrush(Color.White))
                    {
                        g.FillPath(brush, path);
                    }
                    using (Pen pen = new Pen(Theme.CardBorder))
                    {
                        g.DrawPath(pen, path);
                    }
                }
            }
            else if (_hover)
            {
                Rectangle pill = new Rectangle(area.X + 1, area.Y + 1, area.Width - 3, area.Height - 3);
                using (GraphicsPath path = Theme.RoundedRect(pill, 6))
                using (SolidBrush brush = new SolidBrush(Theme.Blend(Theme.Track, Color.White, 0.55)))
                {
                    g.FillPath(brush, path);
                }
            }

            if (_boldFont == null) _boldFont = new Font(Font, FontStyle.Bold);
            Font textFont = Checked ? _boldFont : Font;
            Color textColor = Checked ? Theme.Accent : Theme.TextSecondary;

            TextRenderer.DrawText(g, Text, textFont, area, textColor,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing && _boldFont != null)
            {
                _boldFont.Dispose();
                _boldFont = null;
            }
            base.Dispose(disposing);
        }
    }

    /// <summary>顶部状态胶囊：左侧彩色圆点 + 文字，宽度随文字自适应。</summary>
    public class StatusPill : Control
    {
        private Color _stateColor = Theme.TextMuted;

        public StatusPill()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            Font = Theme.SmallFont;
            BackColor = Theme.HeaderBack;
            Height = 26;
            Width = 120;
        }

        public void SetState(string text, Color stateColor)
        {
            Text = text ?? string.Empty;
            _stateColor = stateColor;
            ResizeToText();
            Invalidate();
        }

        public Color StateColor
        {
            get { return _stateColor; }
        }

        protected override void OnTextChanged(EventArgs e)
        {
            base.OnTextChanged(e);
            ResizeToText();
            Invalidate();
        }

        private void ResizeToText()
        {
            Size measured = TextRenderer.MeasureText(Text ?? string.Empty, Font);
            Width = Math.Max(96, 28 + measured.Width + 12);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            Rectangle bounds = new Rectangle(0, 0, Width - 1, Height - 1);
            using (GraphicsPath path = Theme.RoundedRect(bounds, Height / 2))
            {
                using (SolidBrush fill = new SolidBrush(Theme.Blend(Theme.HeaderBack, Color.White, 0.10)))
                {
                    g.FillPath(fill, path);
                }
                using (Pen pen = new Pen(Theme.Blend(Theme.HeaderBack, Color.White, 0.20)))
                {
                    g.DrawPath(pen, path);
                }
            }

            int dotSize = 8;
            int centerY = Height / 2;
            using (SolidBrush halo = new SolidBrush(Color.FromArgb(70, _stateColor)))
            {
                g.FillEllipse(halo, 11, centerY - 6, 14, 12);
            }
            using (SolidBrush dot = new SolidBrush(_stateColor))
            {
                g.FillEllipse(dot, 14, centerY - dotSize / 2, dotSize, dotSize);
            }

            TextRenderer.DrawText(g, Text, Font,
                new Rectangle(30, 0, Width - 36, Height), Theme.TextOnDark,
                TextFormatFlags.VerticalCenter | TextFormatFlags.Left | TextFormatFlags.EndEllipsis);
        }
    }

    /// <summary>自绘客户端列表：每行一个绿点 + 编号地址，选中用浅蓝底。</summary>
    public class ClientListBox : ListBox
    {
        public ClientListBox()
        {
            SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            DrawMode = DrawMode.OwnerDrawFixed;
            ItemHeight = 28;
            BorderStyle = BorderStyle.None;
            BackColor = Theme.CardBack;
            ForeColor = Theme.TextPrimary;
            Font = Theme.BodyFont;
            IntegralHeight = false;
        }

        protected override void OnDrawItem(DrawItemEventArgs e)
        {
            if (e.Index < 0 || e.Index >= Items.Count) return;

            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            bool selected = (e.State & DrawItemState.Selected) == DrawItemState.Selected;
            Rectangle row = new Rectangle(e.Bounds.X + 2, e.Bounds.Y + 2, e.Bounds.Width - 6, e.Bounds.Height - 4);

            using (GraphicsPath path = Theme.RoundedRect(row, 6))
            using (SolidBrush fill = new SolidBrush(selected ? Theme.AccentTint : Theme.CardBack))
            {
                g.FillPath(fill, path);
            }

            int dotSize = 8;
            using (SolidBrush dot = new SolidBrush(Theme.Success))
            {
                g.FillEllipse(dot, row.X + 8, row.Y + (row.Height - dotSize) / 2, dotSize, dotSize);
            }

            string text = Items[e.Index] == null ? string.Empty : Items[e.Index].ToString();
            Color textColor = !Enabled ? Theme.TextMuted : (selected ? Theme.Accent : Theme.TextPrimary);
            TextRenderer.DrawText(g, text, Font,
                new Rectangle(row.X + 24, row.Y, row.Width - 30, row.Height),
                textColor,
                TextFormatFlags.VerticalCenter | TextFormatFlags.Left | TextFormatFlags.EndEllipsis);
        }
    }
}
