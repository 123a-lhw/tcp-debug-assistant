using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.Windows.Forms;
using TcpTool.App;

namespace TcpTool.Tests
{
    /// <summary>
    /// 窗体验证（无需人眼看图）：
    /// 1) 结构：控件树是否齐全
    /// 2) 布局：有没有控件越界/被裁切，卡片是否堆叠正常
    /// 3) 外观：把窗口渲染成位图，检查配色分布（深色标题栏、白色卡片、无系统默认灰）
    /// 4) 可读性：关键前景/背景色的 WCAG 对比度
    /// 另外支持 --interactive / --server 走一遍真实收发流程。
    /// </summary>
    internal static class GuiHarness
    {
        private static int _failures;

        [STAThread]
        private static int Main(string[] args)
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            ResetSettings();

            if (args != null && args.Length > 0 && args[0] == "--interactive")
            {
                return RunInteractive();
            }
            if (args != null && args.Length > 0 && args[0] == "--server")
            {
                return RunServerMode();
            }

            try
            {
                MainForm form = new MainForm();
                try
                {
                    form.CreateControl();
                    Console.WriteLine("窗体创建成功：标题=" + form.Text);

                    form.Show();
                    Application.DoEvents();
                    Console.WriteLine("窗体显示成功：可见=" + form.Visible + "，尺寸=" + form.Width + "x" + form.Height);

                    List<Control> all = GetAllControls(form);
                    Console.WriteLine("控件总数：" + all.Count);
                    CountControls(all);

                    CheckStructure(form, all);
                    CheckLayout(form, all);
                    CheckContrast();
                    CheckRender(form);

                    Console.WriteLine(_failures == 0 ? "窗体检查全部通过。" : "窗体检查失败 " + _failures + " 项。");
                    return _failures == 0 ? 0 : 1;
                }
                finally
                {
                    form.Close();
                    Application.DoEvents();
                    form.Dispose();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("窗体运行失败：");
                Console.WriteLine(ex.ToString());
                return 2;
            }
        }

        // ---------------- 结构 ----------------

        private static void CountControls(List<Control> all)
        {
            int buttons = 0, textBoxes = 0, checks = 0, combos = 0, labels = 0, lists = 0, radios = 0, cards = 0, pills = 0, headers = 0;
            foreach (Control control in all)
            {
                string type = control.GetType().Name;
                if (type == "FlatButton") buttons++;
                else if (control is RichTextBox) textBoxes++;
                else if (control is TextBox) textBoxes++;
                else if (type == "FlatCheck") checks++;
                else if (control is ComboBox) combos++;
                else if (control is Label) labels++;
                else if (control is ListBox) lists++;
                else if (type == "SegmentRadio") radios++;
                else if (type == "CardPanel") cards++;
                else if (type == "StatusPill") pills++;
                else if (type == "HeaderBar") headers++;
            }

            Console.WriteLine(string.Format(
                "扁平按钮 {0}，输入框 {1}，自绘复选 {2}，下拉 {3}，标签 {4}，列表 {5}，分段单选 {6}，卡片 {7}，状态胶囊 {8}，标题栏 {9}",
                buttons, textBoxes, checks, combos, labels, lists, radios, cards, pills, headers));

            Expect(buttons >= 5, "按钮数量不足：" + buttons);
            Expect(checks >= 6, "自绘复选框数量不足：" + checks);
            Expect(cards >= 6, "卡片数量不足：" + cards);
            Expect(radios == 2, "分段单选应为 2 个");
            Expect(pills == 1, "状态胶囊应为 1 个");
            Expect(headers == 1, "标题栏应为 1 个");
            Expect(lists == 1, "客户端列表应为 1 个");
        }

        private static void CheckStructure(MainForm form, List<Control> all)
        {
            Expect(form.Text == "TCP 调试助手", "窗口标题不对");
            Expect(form.BackColor == Theme.WindowBack, "窗体底色不是主题色");

            RichTextBox log = null;
            TextBox send = null;
            SplitContainer split = null;
            StatusPill pill = null;

            foreach (Control control in all)
            {
                RichTextBox rich = control as RichTextBox;
                if (rich != null && rich.ReadOnly) log = rich;

                TextBox box = control as TextBox;
                if (box != null && box.Multiline && !box.ReadOnly) send = box;

                SplitContainer candidate = control as SplitContainer;
                if (candidate != null) split = candidate;

                StatusPill candidatePill = control as StatusPill;
                if (candidatePill != null) pill = candidatePill;
            }

            Expect(log != null, "找不到接收区 RichTextBox");
            Expect(send != null, "找不到发送区文本框");
            Expect(split != null, "找不到分割容器");
            Expect(pill != null, "找不到状态胶囊");

            if (log != null)
            {
                Expect(log.Font.Name == "Consolas", "接收区应使用等宽字体，实际 " + log.Font.Name);
                Expect(log.BackColor == Theme.LogBack, "接收区底色不对：" + log.BackColor);
                Expect(log.ReadOnly, "接收区应只读");
            }
            if (send != null)
            {
                Expect(send.AcceptsReturn, "发送区应允许回车");
                Expect(send.Text.Length > 0, "发送区应有默认示例内容");
            }
            if (split != null)
            {
                Console.WriteLine("分割条位置：" + split.SplitterDistance +
                    "（接收区 " + split.Panel1.Height + "px / 发送区 " + split.Panel2.Height + "px）");
                Expect(split.Panel1.Height >= 200, "接收区高度偏小：" + split.Panel1.Height);
                Expect(split.Panel2.Height >= 110, "发送区高度偏小：" + split.Panel2.Height);
            }
            if (pill != null)
            {
                Console.WriteLine("状态胶囊：" + pill.Text + "，宽度 " + pill.Width);
                Expect(pill.Text.Length > 0, "状态胶囊没有文字");
            }
        }

        // ---------------- 布局 ----------------

        private static void CheckLayout(MainForm form, List<Control> all)
        {
            Rectangle formArea = form.ClientRectangle;
            int outside = 0;
            int clipped = 0;

            foreach (Control control in all)
            {
                if (!control.Visible) continue;

                Rectangle rect = control.RectangleToScreen(control.ClientRectangle);
                rect = form.RectangleToClient(rect);

                ScrollableControl scrollParent = control.Parent as ScrollableControl;
                bool inScroller = scrollParent != null && scrollParent.AutoScroll;

                if (!formArea.Contains(rect) && !inScroller)
                {
                    outside++;
                    Console.WriteLine(string.Format("  [越界] {0} {1} 超出窗体 {2}",
                        control.GetType().Name, Shorten(control.Text), rect));
                }

                if (inScroller) continue;
                if (control.Parent != null && control.Dock == DockStyle.None)
                {
                    Rectangle parentArea = control.Parent.ClientRectangle;
                    if (!parentArea.Contains(control.Bounds))
                    {
                        clipped++;
                        Console.WriteLine(string.Format("  [裁切] {0} {1} 超出父容器 {2}（控件 {3}）",
                            control.GetType().Name, Shorten(control.Text), parentArea, control.Bounds));
                    }
                }
            }

            Expect(outside == 0, outside + " 个控件超出窗体区域");
            Expect(clipped == 0, clipped + " 个控件被父容器裁切");

            // 左侧卡片自上而下、互不重叠
            List<Control> cards = new List<Control>();
            foreach (Control control in all)
            {
                if (control.GetType().Name == "CardPanel" && control.Parent is FlowLayoutPanel) cards.Add(control);
            }
            cards.Sort(delegate(Control a, Control b) { return a.Top.CompareTo(b.Top); });
            for (int i = 1; i < cards.Count; i++)
            {
                if (cards[i].Top < cards[i - 1].Bottom)
                    Expect(false, "左侧卡片重叠：" + Shorten(cards[i - 1].Text) + " 与 " + Shorten(cards[i].Text));
            }
            if (cards.Count > 0)
            {
                Console.WriteLine("左侧卡片：" + cards.Count + " 张，底部 Y = " + cards[cards.Count - 1].Bottom +
                    "（容器高 " + cards[0].Parent.ClientSize.Height + "）");
                Expect(cards[cards.Count - 1].Bottom <= cards[0].Parent.ClientSize.Height + 1,
                    "左侧卡片总高度超出可视区域，会出现滚动条");
            }

            // 顶部标题栏贴顶且高度正确
            Control header = null;
            foreach (Control control in form.Controls)
            {
                if (control.GetType().Name == "HeaderBar") header = control;
            }
            Expect(header != null, "找不到顶部标题栏");
            if (header != null)
            {
                Expect(header.Top == 0, "标题栏没有贴顶");
                Expect(header.Height >= 48 && header.Height <= 72, "标题栏高度不合适：" + header.Height);
            }
        }

        // ---------------- 可读性（对比度） ----------------

        private static void CheckContrast()
        {
            CheckPair("日志正文", Theme.LogReceived, Theme.LogBack, 7.0);
            CheckPair("日志-发送内容", Theme.LogSent, Theme.LogBack, 4.5);
            CheckPair("日志-时间戳", Theme.LogTime, Theme.LogBack, 4.5);
            CheckPair("日志-客户端编号", Theme.LogWho, Theme.LogBack, 4.5);
            CheckPair("卡片标题", Theme.TextPrimary, Theme.CardBack, 7.0);
            CheckPair("次要文字", Theme.TextSecondary, Theme.CardBack, 4.5);
            CheckPair("输入框文字", Theme.TextPrimary, Theme.InputBack, 7.0);
            CheckPair("主按钮文字", Color.White, Theme.Accent, 4.5);
            CheckPair("危险按钮文字", Color.White, Theme.Danger, 4.5);
            CheckPair("标题栏副标题", Theme.TextMuted, Theme.HeaderBack, 4.5);
            CheckPair("标题栏正文", Theme.TextOnDark, Theme.HeaderBack, 7.0);
        }

        private static void CheckPair(string name, Color foreground, Color background, double minimum)
        {
            double ratio = Theme.ContrastRatio(foreground, background);
            Console.WriteLine(string.Format("  对比度 {0}：{1:0.00}:1（要求 ≥ {2:0.0}）", name, ratio, minimum));
            Expect(ratio >= minimum, string.Format("{0} 对比度不足：{1:0.00}:1 < {2:0.0}", name, ratio, minimum));
        }

        // ---------------- 外观（渲染像素检查） ----------------

        private static void CheckRender(MainForm form)
        {
            string root = AppDomain.CurrentDomain.BaseDirectory;
            string path = System.IO.Path.Combine(root, "ui-render.png");

            Point clientOrigin = form.PointToScreen(Point.Empty);
            int offsetX = clientOrigin.X - form.Left;
            int offsetY = clientOrigin.Y - form.Top;
            Rectangle client = new Rectangle(offsetX, offsetY, form.ClientSize.Width, form.ClientSize.Height);
            Console.WriteLine(string.Format("客户区在截图中的偏移 ({0},{1})，尺寸 {2}x{3}", offsetX, offsetY, client.Width, client.Height));

            Bitmap bitmap = new Bitmap(form.Width, form.Height);
            try
            {
                form.DrawToBitmap(bitmap, new Rectangle(0, 0, form.Width, form.Height));
                bitmap.Save(path, ImageFormat.Png);
            }
            catch (Exception ex)
            {
                Expect(false, "渲染窗口位图失败：" + ex.Message);
                return;
            }
            Console.WriteLine("渲染截图：" + path);

            int white = 0, windowBack = 0, logBack = 0, headerDark = 0, accent = 0, legacyGray = 0, total = 0;
            int grayMinX = -1, grayMaxX = -1, grayMinY = -1, grayMaxY = -1;
            int headerBandSum = 0, headerBandCount = 0;

            for (int y = client.Top; y < client.Bottom; y += 3)
            {
                for (int x = client.Left; x < client.Right; x += 3)
                {
                    Color pixel = bitmap.GetPixel(x, y);
                    total++;

                    if (SameColor(pixel, Color.White)) white++;
                    else if (SameColor(pixel, Theme.WindowBack)) windowBack++;
                    else if (SameColor(pixel, Theme.LogBack)) logBack++;
                    else if (SameColor(pixel, Theme.HeaderBack)) headerDark++;
                    else if (SameColor(pixel, Theme.Accent) || SameColor(pixel, Theme.Danger)) accent++;

                    // WinForms 默认控件灰，出现说明还有没换皮肤的控件
                    if (pixel.R == 240 && pixel.G == 240 && pixel.B == 240)
                    {
                        legacyGray++;
                        if (grayMinX < 0 || x < grayMinX) grayMinX = x;
                        if (x > grayMaxX) grayMaxX = x;
                        if (grayMinY < 0 || y < grayMinY) grayMinY = y;
                        if (y > grayMaxY) grayMaxY = y;
                    }

                    if (y > client.Top + 10 && y < client.Top + 46 && x > client.Left + client.Width / 2 - 150 && x < client.Left + client.Width / 2 + 150)
                    {
                        headerBandSum += (pixel.R + pixel.G + pixel.B) / 3;
                        headerBandCount++;
                    }
                }
            }
            CheckRoundedCorners(form, bitmap);
            bitmap.Dispose();

            Console.WriteLine(string.Format(
                "像素分布：白 {0:0.0}%，页面底色 {1:0.0}%，日志底色 {2:0.0}%，深色标题栏 {3:0.0}%，强调色 {4:0.00}%",
                PercentOf(white, total), PercentOf(windowBack, total), PercentOf(logBack, total),
                PercentOf(headerDark, total), PercentOf(accent, total)));
            Console.WriteLine("标题栏中部平均亮度：" + (headerBandSum / Math.Max(1, headerBandCount)));

            Expect(PercentOf(white, total) > 15, "白色卡片/输入区占比过低，界面可能没有应用卡片样式");
            Expect(PercentOf(windowBack, total) > 3, "页面底色占比过低");
            Expect(PercentOf(logBack, total) > 3, "日志区底色占比过低");
            Expect(PercentOf(headerDark, total) > 1, "深色标题栏占比过低");
            Expect(accent > 0, "没有检测到强调色按钮");
            Expect(headerBandSum / Math.Max(1, headerBandCount) < 60, "标题栏中部不够深，可能是没有绘制");
            if (legacyGray > 0)
            {
                Console.WriteLine(string.Format("系统默认灰像素：{0} 个采样（区域 x {1}..{2}，y {3}..{4}，占客户区 {5:0.00}%）",
                    legacyGray, grayMinX, grayMaxX, grayMinY, grayMaxY, PercentOf(legacyGray, total)));
            }
            Expect(PercentOf(legacyGray, total) < 2.0, "系统默认灰(#F0F0F0)像素过多：" + legacyGray);
        }

        /// <summary>用截图验证卡片确实是圆角：角点为页面底色，内部为白色。</summary>
        private static void CheckRoundedCorners(MainForm form, Bitmap bitmap)
        {
            Control card = null;
            foreach (Control candidate in GetAllControls(form))
            {
                if (candidate.GetType().Name == "CardPanel") { card = candidate; break; }
            }
            if (card == null) return;

            Color corner = PixelAt(form, bitmap, card, 1, 1);
            Color inside = PixelAt(form, bitmap, card, 12, 12);
            Console.WriteLine(string.Format("卡片圆角：角点={0} 内部={1}", Describe(corner), Describe(inside)));

            Expect(SameColor(corner, Theme.WindowBack), "卡片左上角没有圆角，角点应是页面底色");
            Expect(SameColor(inside, Theme.CardBack) || SameColor(inside, Theme.Accent), "卡片内部不是白色");
        }

        private static Color PixelAt(MainForm form, Bitmap bitmap, Control control, int x, int y)
        {
            Point screen = control.PointToScreen(new Point(x, y));
            int bx = screen.X - form.Left;
            int by = screen.Y - form.Top;
            if (bx < 0 || by < 0 || bx >= bitmap.Width || by >= bitmap.Height) return Color.Empty;
            return bitmap.GetPixel(bx, by);
        }

        private static string Describe(Color color)
        {
            return string.Format("#{0:X2}{1:X2}{2:X2}", color.R, color.G, color.B);
        }

        private static double PercentOf(int value, int total)
        {
            return value * 100.0 / Math.Max(1, total);
        }

        private static bool SameColor(Color a, Color b)
        {
            return a.R == b.R && a.G == b.G && a.B == b.B;
        }

        private static string Shorten(string text)
        {
            if (string.IsNullOrEmpty(text)) return string.Empty;
            return text.Length > 14 ? text.Substring(0, 14) : text;
        }

        private static void Expect(bool condition, string message)
        {
            if (condition) return;
            _failures++;
            Console.WriteLine("  [失败] " + message);
        }

        // ---------------- 交互模式：客户端流程 ----------------

        private static int RunInteractive()
        {
            System.Net.Sockets.TcpListener listener = null;
            System.Net.Sockets.TcpClient peer = null;
            MainForm form = null;

            try
            {
                form = new MainForm();
                form.Show();
                Pump(500);

                int port = GetPortField(form);
                Console.WriteLine("界面端口设置：" + port);

                listener = new System.Net.Sockets.TcpListener(System.Net.IPAddress.Loopback, port);
                listener.Start(4);

                Button connect = FindButton(form, "连接");
                if (connect == null) throw new Exception("找不到「连接」按钮，现有按钮：" + DescribeButtons(form));
                Console.WriteLine("点击「连接」…");
                connect.PerformClick();
                Pump(300);

                IAsyncResult pending = listener.BeginAcceptTcpClient(null, null);
                for (int i = 0; i < 100 && !pending.AsyncWaitHandle.WaitOne(50); i++) Pump(50);
                if (!pending.AsyncWaitHandle.WaitOne(0)) throw new Exception("界面上没有发起 TCP 连接");
                peer = listener.EndAcceptTcpClient(pending);
                Console.WriteLine("对端已接受连接：" + peer.Client.RemoteEndPoint);

                Pump(600);
                Console.WriteLine("连接按钮现在的文字：" + connect.Text);
                Console.WriteLine("状态胶囊：" + DescribeStatus(form));

                Button send = FindButtonStartsWith(form, "发送");
                if (send == null) throw new Exception("找不到「发送」按钮");
                Console.WriteLine("点击「发送」…");
                send.PerformClick();
                Pump(300);

                peer.GetStream().ReadTimeout = 3000;
                byte[] buffer = new byte[256];
                int read = peer.GetStream().Read(buffer, 0, buffer.Length);
                Console.WriteLine(string.Format("对端收到 {0} 字节：{1}", read, System.Text.Encoding.UTF8.GetString(buffer, 0, read)));

                Pump(500);
                RichTextBox receive = FindReceiveBox(form);
                string shown = receive == null ? "(找不到)" : receive.Text.Replace("\r\n", "\\n");
                Console.WriteLine("接收区内容：" + shown);

                SaveRender(form, "ui-render-chat.png");

                bool ok = read > 0 && receive != null && receive.Text.Contains("hello tcp");
                Console.WriteLine(ok ? "交互流程检查通过。" : "交互流程检查失败。");
                return ok ? 0 : 1;
            }
            catch (Exception ex)
            {
                Console.WriteLine("交互流程异常：");
                Console.WriteLine(ex.ToString());
                return 2;
            }
            finally
            {
                if (peer != null) peer.Close();
                if (listener != null) listener.Stop();
                if (form != null)
                {
                    form.Close();
                    Pump(200);
                    form.Dispose();
                }
            }
        }

        // ---------------- 服务端模式：监听、客户端列表、广播 ----------------

        private static int RunServerMode()
        {
            MainForm form = null;
            System.Net.Sockets.TcpClient peer = null;

            try
            {
                form = new MainForm();
                form.Show();
                Pump(400);

                RadioButton serverRadio = FindRadio(form, "TCP 服务端");
                if (serverRadio == null) throw new Exception("找不到「TCP 服务端」分段按钮");
                serverRadio.Checked = true;
                Pump(300);

                int port = GetPortField(form);
                Button listen = FindButton(form, "开始监听");
                if (listen == null) throw new Exception("找不到「开始监听」按钮，现有按钮：" + DescribeButtons(form));
                Console.WriteLine("点击「开始监听」(" + port + ") …");
                listen.PerformClick();
                Pump(400);
                Console.WriteLine("按钮现在：" + listen.Text + "，状态胶囊：" + DescribeStatus(form));

                peer = new System.Net.Sockets.TcpClient();
                peer.Connect(System.Net.IPAddress.Loopback, port);
                Pump(600);

                ListBox clients = FindListBox(form);
                if (clients == null) throw new Exception("找不到客户端列表");
                Console.WriteLine("客户端列表项数：" + clients.Items.Count +
                    (clients.Items.Count > 0 ? "，第一项：" + clients.Items[0] : ""));

                Button send = FindButtonStartsWith(form, "发送");
                if (send == null) throw new Exception("找不到「发送」按钮");
                Console.WriteLine("点击「发送」（广播）…");
                send.PerformClick();
                Pump(200);

                System.Net.Sockets.NetworkStream stream = peer.GetStream();
                stream.ReadTimeout = 3000;
                byte[] buffer = new byte[256];
                int read = stream.Read(buffer, 0, buffer.Length);
                string broadcastText = System.Text.Encoding.UTF8.GetString(buffer, 0, read);
                Console.WriteLine(string.Format("外部客户端收到 {0} 字节：{1}", read, broadcastText));

                byte[] ping = System.Text.Encoding.UTF8.GetBytes("ping-from-peer");
                stream.Write(ping, 0, ping.Length);
                stream.Flush();
                Pump(700);

                RichTextBox receive = FindReceiveBox(form);
                string shown = receive == null ? "(找不到接收区)" : receive.Text.Replace("\r\n", "\\n");
                Console.WriteLine("接收区内容：" + shown);

                Button stop = FindButton(form, "停止监听");
                if (stop == null) throw new Exception("找不到「停止监听」按钮，现有按钮：" + DescribeButtons(form));
                stop.PerformClick();
                Pump(600);
                Console.WriteLine("停止监听后客户端列表项数：" + clients.Items.Count +
                    "，按钮：" + (FindButton(form, "开始监听") != null ? "开始监听" : "?"));

                bool ok = clients.Items.Count == 0
                    && broadcastText == "hello tcp"
                    && shown.Contains("ping-from-peer")
                    && FindButton(form, "开始监听") != null;

                Console.WriteLine(ok ? "服务端模式检查通过。" : "服务端模式检查失败。");
                return ok ? 0 : 1;
            }
            catch (Exception ex)
            {
                Console.WriteLine("服务端模式异常：");
                Console.WriteLine(ex.ToString());
                return 2;
            }
            finally
            {
                if (peer != null) peer.Close();
                if (form != null)
                {
                    form.Close();
                    Pump(200);
                    form.Dispose();
                }
            }
        }

        // ---------------- 小工具 ----------------

        /// <summary>把当前窗口渲染成 PNG，方便人眼复查界面。</summary>
        private static void SaveRender(Control form, string fileName)
        {
            try
            {
                string path = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, fileName);
                using (Bitmap bitmap = new Bitmap(form.Width, form.Height))
                {
                    form.DrawToBitmap(bitmap, new Rectangle(0, 0, form.Width, form.Height));
                    bitmap.Save(path, ImageFormat.Png);
                }
                Console.WriteLine("已保存界面渲染图：" + path);
            }
            catch (Exception ex)
            {
                Console.WriteLine("保存渲染图失败（忽略）：" + ex.Message);
            }
        }

        /// <summary>测试前清掉保存的设置，保证每次都是默认配置。</summary>
        private static void ResetSettings()
        {
            try
            {
                string dir = System.IO.Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "TcpDebugAssistant");
                string file = System.IO.Path.Combine(dir, "settings.ini");
                if (System.IO.File.Exists(file)) System.IO.File.Delete(file);
                Console.WriteLine("已重置界面设置：" + file);
            }
            catch (Exception ex)
            {
                Console.WriteLine("重置设置失败（忽略）：" + ex.Message);
            }
        }

        private static void Pump(int milliseconds)
        {
            DateTime deadline = DateTime.Now.AddMilliseconds(milliseconds);
            do
            {
                Application.DoEvents();
                System.Threading.Thread.Sleep(10);
            }
            while (DateTime.Now < deadline);
        }

        private static int GetPortField(Control root)
        {
            foreach (Control control in GetAllControls(root))
            {
                NumericUpDown numeric = control as NumericUpDown;
                if (numeric != null && numeric.Maximum == 65535) return (int)numeric.Value;
            }
            return 8000;
        }

        private static string DescribeStatus(Control root)
        {
            foreach (Control control in GetAllControls(root))
            {
                StatusPill pill = control as StatusPill;
                if (pill != null) return pill.Text;
            }
            return "(无)";
        }

        private static string DescribeButtons(Control root)
        {
            List<string> names = new List<string>();
            foreach (Control control in GetAllControls(root))
            {
                Button button = control as Button;
                if (button != null) names.Add(button.Text);
            }
            return string.Join(" | ", names.ToArray());
        }

        private static RichTextBox FindReceiveBox(Control root)
        {
            foreach (Control control in GetAllControls(root))
            {
                RichTextBox rich = control as RichTextBox;
                if (rich != null && rich.ReadOnly) return rich;
            }
            return null;
        }

        private static ListBox FindListBox(Control root)
        {
            foreach (Control control in GetAllControls(root))
            {
                ListBox list = control as ListBox;
                if (list != null) return list;
            }
            return null;
        }

        private static RadioButton FindRadio(Control root, string text)
        {
            foreach (Control control in GetAllControls(root))
            {
                RadioButton radio = control as RadioButton;
                if (radio != null && radio.Text == text) return radio;
            }
            return null;
        }

        private static Button FindButton(Control root, string text)
        {
            foreach (Control control in GetAllControls(root))
            {
                Button button = control as Button;
                if (button != null && button.Text == text) return button;
            }
            return null;
        }

        private static Button FindButtonStartsWith(Control root, string prefix)
        {
            foreach (Control control in GetAllControls(root))
            {
                Button button = control as Button;
                if (button != null && button.Text.StartsWith(prefix, StringComparison.Ordinal)) return button;
            }
            return null;
        }

        private static List<Control> GetAllControls(Control parent)
        {
            List<Control> result = new List<Control>();
            Collect(parent, result);
            return result;
        }

        private static void Collect(Control parent, List<Control> sink)
        {
            foreach (Control child in parent.Controls)
            {
                sink.Add(child);
                Collect(child, sink);
            }
        }
    }
}
