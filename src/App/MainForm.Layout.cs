using System;
using System.Drawing;
using System.Windows.Forms;
using TcpTool.Core;

namespace TcpTool.App
{
    /// <summary>界面控件的创建与摆放（等价于设计器生成的代码）。</summary>
    public sealed partial class MainForm
    {
        private const int CardWidth = 300;
        private const int CardGap = 10;

        // ① 连接
        private SegmentRadio rbClient;
        private SegmentRadio rbServer;
        private Label lblHost;
        private Label lblPort;
        private TextBox txtHost;
        private NumericUpDown numPort;
        private FlatButton btnConnect;

        // ② 接收显示
        private ComboBox cboEncoding;
        private CheckBox chkShowHex;
        private CheckBox chkTimestamp;
        private CheckBox chkEchoSent;

        // ③ 发送选项
        private CheckBox chkSendHex;
        private ComboBox cboNewLine;
        private CheckBox chkAutoSend;
        private NumericUpDown numInterval;
        private CheckBox chkBroadcast;

        // ④ 客户端列表
        private Label lblClientCount;
        private ListBox lstClients;

        // 接收区
        private FlatButton btnClearRecv;
        private FlatButton btnSaveLog;
        private CheckBox chkPause;
        private CheckBox chkAutoScroll;
        private RichTextBox txtRecv;

        // 发送区
        private ComboBox cboHistory;
        private TextBox txtSend;
        private FlatButton btnSend;
        private FlatButton btnClearSend;
        private Label lblHint;

        // 顶部信息栏
        private StatusPill statusPill;
        private Label lblRecvBytes;
        private Label lblSentBytes;
        private Label lblClients;
        private Label lblTitle;

        private SplitContainer _rightSplit;

        private void InitializeComponent()
        {
            SuspendLayout();

            AutoScaleMode = AutoScaleMode.Font;
            Font = Theme.BodyFont;
            BackColor = Theme.WindowBack;
            ForeColor = Theme.TextPrimary;
            Text = "TCP 调试助手";
            StartPosition = FormStartPosition.CenterScreen;
            ClientSize = new Size(1200, 860);
            MinimumSize = new Size(1040, 700);
            DoubleBuffered = true;

            // ---------- 顶部信息栏 ----------
            HeaderBar header = new HeaderBar();
            header.Dock = DockStyle.Top;
            header.Height = 56;

            lblTitle = new Label();
            lblTitle.Text = "TCP 调试助手";
            lblTitle.Font = Theme.TitleFont;
            lblTitle.ForeColor = Color.White;
            lblTitle.BackColor = Theme.HeaderBack;
            lblTitle.AutoSize = true;
            lblTitle.Location = new Point(18, 8);

            Label lblSubtitle = new Label();
            lblSubtitle.Text = "TCP Debug Assistant · 客户端 / 服务端调试";
            lblSubtitle.Font = Theme.SmallFont;
            lblSubtitle.ForeColor = Theme.TextMuted;
            lblSubtitle.BackColor = Theme.HeaderBack;
            lblSubtitle.AutoSize = true;
            lblSubtitle.Location = new Point(19, 34);

            FlowLayoutPanel headerRight = new FlowLayoutPanel();
            headerRight.Dock = DockStyle.Right;
            headerRight.FlowDirection = FlowDirection.RightToLeft;
            headerRight.WrapContents = false;
            headerRight.AutoSize = true;
            headerRight.AutoSizeMode = AutoSizeMode.GrowAndShrink;
            headerRight.BackColor = Theme.HeaderBack;
            headerRight.Padding = new Padding(0, 15, 16, 0);

            statusPill = new StatusPill();
            statusPill.Margin = new Padding(0, 0, 14, 0);
            statusPill.SetState("未连接", Theme.TextMuted);

            lblRecvBytes = MakeHeaderLabel("↓ 0 B");
            lblSentBytes = MakeHeaderLabel("↑ 0 B");
            lblClients = MakeHeaderLabel("客户端 0");

            // RightToLeft 布局：先加的排在更右侧
            headerRight.Controls.Add(statusPill);
            headerRight.Controls.Add(lblClients);
            headerRight.Controls.Add(lblSentBytes);
            headerRight.Controls.Add(lblRecvBytes);

            header.Controls.Add(lblTitle);
            header.Controls.Add(lblSubtitle);
            header.Controls.Add(headerRight);

            // ---------- 主体 ----------
            Panel main = new Panel();
            main.Dock = DockStyle.Fill;
            main.BackColor = Theme.WindowBack;
            main.Padding = new Padding(12, 12, 12, 12);

            // 右侧：接收区 / 发送区
            SplitContainer rightSplit = new SplitContainer();
            rightSplit.Dock = DockStyle.Fill;
            rightSplit.Orientation = Orientation.Horizontal;
            rightSplit.SplitterWidth = 10;
            rightSplit.BackColor = Theme.WindowBack;
            rightSplit.Panel1.BackColor = Theme.WindowBack;
            rightSplit.Panel2.BackColor = Theme.WindowBack;
            _rightSplit = rightSplit;

            rightSplit.Panel1.Controls.Add(BuildReceiveCard());
            rightSplit.Panel2.Controls.Add(BuildSendCard());

            // 左侧：设置卡片
            Panel leftPanel = new Panel();
            leftPanel.Dock = DockStyle.Left;
            leftPanel.Width = CardWidth + 24;
            leftPanel.Padding = new Padding(0, 0, 12, 0);
            leftPanel.BackColor = Theme.WindowBack;

            FlowLayoutPanel leftFlow = new FlowLayoutPanel();
            leftFlow.Dock = DockStyle.Fill;
            leftFlow.FlowDirection = FlowDirection.TopDown;
            leftFlow.WrapContents = false;
            leftFlow.AutoScroll = true;
            leftFlow.BackColor = Theme.WindowBack;
            leftPanel.Controls.Add(leftFlow);

            leftFlow.Controls.Add(BuildConnectionCard());
            leftFlow.Controls.Add(BuildDisplayCard());
            leftFlow.Controls.Add(BuildSendOptionsCard());
            leftFlow.Controls.Add(BuildClientsCard());

            main.Controls.Add(rightSplit);
            main.Controls.Add(leftPanel);

            Controls.Add(main);
            Controls.Add(header);

            ResumeLayout(false);
            PerformLayout();
        }

        // ---------- 各卡片 ----------

        private CardPanel BuildConnectionCard()
        {
            CardPanel card = NewCard("连接", 214);

            SegmentGroup segments = new SegmentGroup();
            segments.Location = new Point(14, 42);
            segments.Size = new Size(CardWidth - 28, 40);

            rbClient = new SegmentRadio();
            rbClient.Text = "TCP 客户端";
            rbClient.Bounds = new Rectangle(2, 2, segments.Width / 2 - 2, 36);
            rbClient.RoundLeft = true;
            rbClient.RoundRight = false;
            rbClient.Checked = true;
            rbClient.CheckedChanged += Mode_CheckedChanged;

            rbServer = new SegmentRadio();
            rbServer.Text = "TCP 服务端";
            rbServer.Bounds = new Rectangle(segments.Width / 2, 2, segments.Width - segments.Width / 2 - 2, 36);
            rbServer.RoundLeft = false;
            rbServer.RoundRight = true;
            rbServer.CheckedChanged += Mode_CheckedChanged;

            segments.Controls.Add(rbClient);
            segments.Controls.Add(rbServer);

            lblHost = MakeCaption("远程主机", 14, 92);
            lblPort = MakeCaption("端口", 206, 92);
            lblPort.Size = new Size(80, 18);

            Frame hostFrame = NewInputFrame(new Point(14, 112), new Size(184, 34));
            txtHost = NewInnerTextBox("127.0.0.1");
            hostFrame.Controls.Add(txtHost);
            WireFocusFrame(hostFrame, txtHost);

            Frame portFrame = NewInputFrame(new Point(206, 112), new Size(80, 34));
            numPort = new NumericUpDown();
            numPort.BorderStyle = BorderStyle.None;
            numPort.Font = Theme.BodyFont;
            numPort.BackColor = Theme.InputBack;
            numPort.ForeColor = Theme.TextPrimary;
            numPort.Minimum = 1;
            numPort.Maximum = 65535;
            numPort.Value = 8000;
            numPort.Dock = DockStyle.Fill;
            portFrame.Controls.Add(numPort);
            WireFocusFrame(portFrame, numPort);

            btnConnect = new FlatButton();
            btnConnect.Text = "连接";
            btnConnect.Location = new Point(14, 158);
            btnConnect.Size = new Size(CardWidth - 28, 38);
            btnConnect.Click += btnConnect_Click;

            card.Controls.Add(segments);
            card.Controls.Add(lblHost);
            card.Controls.Add(lblPort);
            card.Controls.Add(hostFrame);
            card.Controls.Add(portFrame);
            card.Controls.Add(btnConnect);
            return card;
        }

        private CardPanel BuildDisplayCard()
        {
            CardPanel card = NewCard("接收显示", 150);

            Label lblEncoding = MakeCaption("编码", 14, 46);
            lblEncoding.Size = new Size(40, 26);

            cboEncoding = NewCombo(new Point(58, 46), new Size(CardWidth - 58 - 14, 26));
            cboEncoding.Items.AddRange(TextCodec.Names);
            cboEncoding.SelectedIndex = 0;
            cboEncoding.SelectedIndexChanged += cboEncoding_SelectedIndexChanged;

            chkShowHex = NewCheck("接收区按十六进制显示", new Point(14, 78), CardWidth - 28);
            chkShowHex.CheckedChanged += chkShowHex_CheckedChanged;

            chkTimestamp = NewCheck("显示时间戳", new Point(14, 106), 122);
            chkTimestamp.Checked = true;
            chkEchoSent = NewCheck("显示发送内容", new Point(142, 106), 144);
            chkEchoSent.Checked = true;

            card.Controls.Add(lblEncoding);
            card.Controls.Add(cboEncoding);
            card.Controls.Add(chkShowHex);
            card.Controls.Add(chkTimestamp);
            card.Controls.Add(chkEchoSent);
            return card;
        }

        private CardPanel BuildSendOptionsCard()
        {
            CardPanel card = NewCard("发送选项", 182);

            chkSendHex = NewCheck("十六进制发送", new Point(14, 44), CardWidth - 28);
            chkSendHex.CheckedChanged += chkSendHex_CheckedChanged;

            Label lblNewLine = MakeCaption("换行", 14, 74);
            lblNewLine.Size = new Size(40, 26);

            cboNewLine = NewCombo(new Point(58, 74), new Size(CardWidth - 58 - 14, 26));
            cboNewLine.Items.Add("不追加");
            cboNewLine.Items.Add("LF (\\n)");
            cboNewLine.Items.Add("CRLF (\\r\\n)");
            cboNewLine.SelectedIndex = 0;

            chkAutoSend = NewCheck("定时发送", new Point(14, 108), 82);
            chkAutoSend.CheckedChanged += chkAutoSend_CheckedChanged;

            Frame intervalFrame = NewInputFrame(new Point(98, 104), new Size(86, 32));
            numInterval = new NumericUpDown();
            numInterval.BorderStyle = BorderStyle.None;
            numInterval.Font = Theme.BodyFont;
            numInterval.BackColor = Theme.InputBack;
            numInterval.ForeColor = Theme.TextPrimary;
            numInterval.Minimum = 10;
            numInterval.Maximum = 600000;
            numInterval.Increment = 50;
            numInterval.Value = 1000;
            numInterval.Dock = DockStyle.Fill;
            numInterval.ValueChanged += numInterval_ValueChanged;
            intervalFrame.Controls.Add(numInterval);
            WireFocusFrame(intervalFrame, numInterval);

            Label lblMs = MakeCaption("毫秒", 190, 108);
            lblMs.Size = new Size(44, 24);

            chkBroadcast = NewCheck("广播给所有客户端", new Point(14, 142), CardWidth - 28);
            chkBroadcast.Checked = true;
            chkBroadcast.CheckedChanged += chkBroadcast_CheckedChanged;

            card.Controls.Add(chkSendHex);
            card.Controls.Add(lblNewLine);
            card.Controls.Add(cboNewLine);
            card.Controls.Add(chkAutoSend);
            card.Controls.Add(intervalFrame);
            card.Controls.Add(lblMs);
            card.Controls.Add(chkBroadcast);
            return card;
        }

        private CardPanel BuildClientsCard()
        {
            CardPanel card = NewCard("客户端", 184);

            lblClientCount = new Label();
            lblClientCount.Text = "共 0 个客户端";
            lblClientCount.Font = Theme.SmallFont;
            lblClientCount.ForeColor = Theme.TextSecondary;
            lblClientCount.BackColor = Theme.CardBack;
            lblClientCount.AutoSize = false;
            lblClientCount.Location = new Point(14, 44);
            lblClientCount.Size = new Size(CardWidth - 28, 16);

            ClientListBox list = new ClientListBox();
            list.Location = new Point(14, 64);
            list.Size = new Size(CardWidth - 28, 106);
            list.SelectionMode = SelectionMode.MultiExtended;
            lstClients = list;

            card.Controls.Add(lblClientCount);
            card.Controls.Add(list);
            return card;
        }

        private CardPanel BuildReceiveCard()
        {
            CardPanel card = new CardPanel();
            card.Title = "接收";
            card.Dock = DockStyle.Fill;
            card.Padding = new Padding(14, 46, 14, 14);

            FlowLayoutPanel toolbar = new FlowLayoutPanel();
            toolbar.Dock = DockStyle.Top;
            toolbar.Height = 32;
            toolbar.WrapContents = false;
            toolbar.BackColor = Theme.CardBack;

            btnClearRecv = NewSmallButton("清空", FlatButton.Kind.Ghost);
            btnClearRecv.Click += btnClearRecv_Click;

            btnSaveLog = NewSmallButton("保存日志…", FlatButton.Kind.Ghost);
            btnSaveLog.Width = 96;
            btnSaveLog.Click += btnSaveLog_Click;

            chkPause = NewCheck("暂停显示", Point.Empty, 92);
            chkPause.Margin = new Padding(8, 5, 0, 0);
            chkPause.CheckedChanged += chkPause_CheckedChanged;

            chkAutoScroll = NewCheck("自动滚动", Point.Empty, 92);
            chkAutoScroll.Margin = new Padding(6, 5, 0, 0);
            chkAutoScroll.Checked = true;
            chkAutoScroll.CheckedChanged += chkAutoScroll_CheckedChanged;

            toolbar.Controls.Add(btnClearRecv);
            toolbar.Controls.Add(btnSaveLog);
            toolbar.Controls.Add(chkPause);
            toolbar.Controls.Add(chkAutoScroll);

            Frame logFrame = new Frame();
            logFrame.Dock = DockStyle.Fill;
            logFrame.FillColor = Theme.LogBack;
            logFrame.BorderColor = Theme.CardBorder;
            logFrame.Radius = 8;
            logFrame.Padding = new Padding(10, 8, 8, 8);
            logFrame.Margin = new Padding(0, 6, 0, 0);

            txtRecv = new RichTextBox();
            txtRecv.Dock = DockStyle.Fill;
            txtRecv.BorderStyle = BorderStyle.None;
            txtRecv.BackColor = Theme.LogBack;
            txtRecv.ForeColor = Theme.LogReceived;
            txtRecv.Font = Theme.MonoFont;
            txtRecv.ReadOnly = true;
            txtRecv.WordWrap = false;
            txtRecv.ScrollBars = RichTextBoxScrollBars.Both;
            txtRecv.HideSelection = false;
            txtRecv.DetectUrls = false;

            logFrame.Controls.Add(txtRecv);

            card.Controls.Add(logFrame);
            card.Controls.Add(toolbar);
            return card;
        }

        private CardPanel BuildSendCard()
        {
            CardPanel card = new CardPanel();
            card.Title = "发送";
            card.Dock = DockStyle.Fill;
            card.Padding = new Padding(14, 46, 14, 14);

            Panel historyBar = new Panel();
            historyBar.Dock = DockStyle.Top;
            historyBar.Height = 32;
            historyBar.BackColor = Theme.CardBack;

            Label lblHistory = new Label();
            lblHistory.Text = "历史";
            lblHistory.Font = Theme.BodyFont;
            lblHistory.ForeColor = Theme.TextSecondary;
            lblHistory.BackColor = Theme.CardBack;
            lblHistory.AutoSize = true;
            lblHistory.Location = new Point(0, 7);

            cboHistory = NewCombo(new Point(38, 4), new Size(340, 26));
            cboHistory.SelectedIndexChanged += cboHistory_SelectedIndexChanged;

            historyBar.Controls.Add(lblHistory);
            historyBar.Controls.Add(cboHistory);

            FlowLayoutPanel buttons = new FlowLayoutPanel();
            buttons.Dock = DockStyle.Bottom;
            buttons.Height = 52;
            buttons.WrapContents = false;
            buttons.BackColor = Theme.CardBack;
            buttons.Padding = new Padding(0, 12, 0, 0);

            btnSend = new FlatButton();
            btnSend.Text = "发送 (Ctrl+Enter)";
            btnSend.ButtonKind = FlatButton.Kind.Primary;
            btnSend.Size = new Size(150, 36);
            btnSend.Margin = new Padding(0, 0, 8, 0);
            btnSend.Click += btnSend_Click;

            btnClearSend = new FlatButton();
            btnClearSend.Text = "清空";
            btnClearSend.ButtonKind = FlatButton.Kind.Ghost;
            btnClearSend.Size = new Size(74, 36);
            btnClearSend.Margin = new Padding(0, 0, 12, 0);
            btnClearSend.Click += btnClearSend_Click;

            lblHint = new Label();
            lblHint.Text = "提示：十六进制发送时，换行符按 ASCII 追加";
            lblHint.Font = Theme.SmallFont;
            lblHint.ForeColor = Theme.TextSecondary;
            lblHint.BackColor = Theme.CardBack;
            lblHint.AutoSize = true;
            lblHint.Margin = new Padding(0, 10, 0, 0);

            buttons.Controls.Add(btnSend);
            buttons.Controls.Add(btnClearSend);
            buttons.Controls.Add(lblHint);

            FlatInput sendInput = new FlatInput(true);
            sendInput.Dock = DockStyle.Fill;
            sendInput.Margin = new Padding(0, 6, 0, 0);
            txtSend = sendInput.Inner;
            txtSend.ScrollBars = ScrollBars.None;   // 发送区不显示系统滚动条，保持干净
            txtSend.Text = "hello tcp";
            txtSend.KeyDown += txtSend_KeyDown;

            card.Controls.Add(sendInput);
            card.Controls.Add(historyBar);
            card.Controls.Add(buttons);
            return card;
        }

        // ---------- 工厂方法 ----------

        private static CardPanel NewCard(string title, int height)
        {
            CardPanel card = new CardPanel();
            card.Title = title;
            card.Size = new Size(CardWidth, height);
            card.Margin = new Padding(0, 0, 0, CardGap);
            card.Padding = Padding.Empty;
            return card;
        }

        private static Label MakeCaption(string text, int x, int y)
        {
            Label label = new Label();
            label.Text = text;
            label.Font = Theme.SmallFont;
            label.ForeColor = Theme.TextSecondary;
            label.BackColor = Theme.CardBack;
            label.AutoSize = false;
            label.Location = new Point(x, y);
            label.Size = new Size(120, 18);
            label.TextAlign = ContentAlignment.MiddleLeft;
            return label;
        }

        private static Frame NewInputFrame(Point location, Size size)
        {
            Frame frame = new Frame();
            frame.Location = location;
            frame.Size = size;
            frame.FillColor = Theme.InputBack;
            frame.BorderColor = Theme.InputBorder;
            frame.Radius = 8;
            frame.Padding = new Padding(10, 6, 6, 6);
            return frame;
        }

        private static TextBox NewInnerTextBox(string text)
        {
            TextBox box = new TextBox();
            box.Text = text;
            box.BorderStyle = BorderStyle.None;
            box.BackColor = Theme.InputBack;
            box.ForeColor = Theme.TextPrimary;
            box.Font = Theme.BodyFont;
            box.Dock = DockStyle.Fill;
            return box;
        }

        private static void WireFocusFrame(Frame frame, Control inner)
        {
            inner.GotFocus += delegate
            {
                frame.BorderColor = Theme.Accent;
                frame.Invalidate();
            };
            inner.LostFocus += delegate
            {
                frame.BorderColor = Theme.InputBorder;
                frame.Invalidate();
            };
        }

        private static ComboBox NewCombo(Point location, Size size)
        {
            ComboBox combo = new ComboBox();
            combo.Location = location;
            combo.Size = size;
            combo.DropDownStyle = ComboBoxStyle.DropDownList;
            combo.FlatStyle = FlatStyle.Flat;
            combo.BackColor = Theme.InputBack;
            combo.ForeColor = Theme.TextPrimary;
            combo.Font = Theme.BodyFont;
            return combo;
        }

        private static CheckBox NewCheck(string text, Point location, int width)
        {
            FlatCheck check = new FlatCheck();
            check.Text = text;
            check.Location = location;
            check.Width = width;
            check.BackColor = Theme.CardBack;
            check.ForeColor = Theme.TextPrimary;
            return check;
        }

        private static FlatButton NewSmallButton(string text, FlatButton.Kind kind)
        {
            FlatButton button = new FlatButton();
            button.Text = text;
            button.ButtonKind = kind;
            button.Size = new Size(62, 26);
            button.Margin = new Padding(0, 2, 8, 0);
            return button;
        }

        private static Label MakeHeaderLabel(string text)
        {
            Label label = new Label();
            label.Text = text;
            label.Font = Theme.SmallFont;
            label.ForeColor = Theme.TextOnDark;
            label.BackColor = Theme.HeaderBack;
            label.AutoSize = true;
            label.Margin = new Padding(0, 6, 16, 0);
            return label;
        }
    }
}
