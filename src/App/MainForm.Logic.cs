using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using TcpTool.Core;

namespace TcpTool.App
{
    /// <summary>主窗口的交互逻辑：连接管理、收发、显示、设置。</summary>
    public sealed partial class MainForm : Form
    {
        private const int ConnectTimeoutMs = 5000;
        private const int MaxRecvChars = 2000000;
        private const int HistoryLimit = 20;

        private readonly TcpServer _server;
        private readonly TcpClientSession _client;
        private readonly TrafficStats _stats;
        private readonly StreamTextDecoder _clientDecoder;
        private readonly Dictionary<long, StreamTextDecoder> _serverDecoders;
        private readonly List<LogSegment> _recvPending;
        private readonly List<string> _history;
        private System.Windows.Forms.Timer _flushTimer;
        private System.Windows.Forms.Timer _autoSendTimer;

        private bool _suppressHistoryEvent;
        private bool _connecting;
        private string _savedHost = "127.0.0.1";

        public MainForm()
        {
            _server = new TcpServer();
            _client = new TcpClientSession();
            _stats = new TrafficStats();
            _clientDecoder = new StreamTextDecoder(TextCodec.Utf8);
            _serverDecoders = new Dictionary<long, StreamTextDecoder>();
            _recvPending = new List<LogSegment>(64);
            _history = new List<string>();

            InitializeComponent();

            _flushTimer = new System.Windows.Forms.Timer();
            _flushTimer.Interval = 60;
            _flushTimer.Tick += FlushRecvPending;

            _autoSendTimer = new System.Windows.Forms.Timer();
            _autoSendTimer.Interval = 1000;
            _autoSendTimer.Tick += AutoSendTimer_Tick;

            WireEvents();
            ApplySettings(SettingsStore.Load());
            UpdateModeUi();
            UpdateStatsUi();
            _flushTimer.Start();
        }

        /// <summary>窗口显示后再设置分割条位置：此时容器才有真实尺寸。</summary>
        protected override void OnLoad(EventArgs e)
        {
            base.OnLoad(e);
            ApplySplitterLayout();
        }

        private void ApplySplitterLayout()
        {
            if (_rightSplit == null) return;

            int height = _rightSplit.Height;
            if (height < 240) return;

            try
            {
                _rightSplit.Panel1MinSize = 50;
                _rightSplit.Panel2MinSize = 50;

                int distance = (int)(height * 0.58);
                int maxDistance = height - 110 - _rightSplit.SplitterWidth;
                if (distance > maxDistance) distance = maxDistance;
                if (distance < 120) distance = 120;
                _rightSplit.SplitterDistance = distance;

                _rightSplit.Panel1MinSize = 120;
                _rightSplit.Panel2MinSize = 110;
            }
            catch (InvalidOperationException)
            {
                // 极端尺寸下放弃精细布局，保留默认分割位置
            }
            catch (ArgumentOutOfRangeException)
            {
            }
        }

        private void WireEvents()
        {            _server.Started += Server_Started;
            _server.Stopped += Server_Stopped;
            _server.ClientConnected += Server_ClientConnected;
            _server.ClientDisconnected += Server_ClientDisconnected;
            _server.DataReceived += Server_DataReceived;
            _server.Failed += Server_Failed;

            _client.Connected += Client_Connected;
            _client.Closed += Client_Closed;
            _client.DataReceived += Client_DataReceived;
            _client.Failed += Client_Failed;

            FormClosing += MainForm_FormClosing;
        }

        // ===================== 连接管理 =====================

        private void btnConnect_Click(object sender, EventArgs e)
        {
            if (_connecting) return;

            if (_server.IsRunning)
            {
                StopServer();
                return;
            }
            if (_client.IsConnected)
            {
                _client.Close();
                SetState("○ 已断开连接", Theme.TextMuted);
                UpdateModeUi();
                return;
            }

            if (rbServer.Checked) StartServer();
            else ConnectClient();
        }

        private void StartServer()
        {
            int port = (int)numPort.Value;
            try
            {
                _server.Start(port);
            }
            catch (SocketException ex)
            {
                ShowWarn("启动监听失败：" + ex.Message + "\r\n端口可能已被其它程序占用，换一个端口试试。");
                return;
            }
            catch (Exception ex)
            {
                ShowWarn("启动监听失败：" + ex.Message);
                return;
            }

            lstClients.Items.Clear();
            _serverDecoders.Clear();
            UpdateModeUi();
            UpdateClientCount();
            SetHint("已开始监听，等待客户端接入…");
        }

        private void StopServer()
        {
            try
            {
                _server.Stop();
            }
            catch (Exception ex)
            {
                SetHint("停止监听时出错：" + ex.Message);
            }
            UpdateModeUi();
            SetHint("已停止监听");
        }

        private void ConnectClient()
        {
            string host = txtHost.Text.Trim();
            int port = (int)numPort.Value;

            if (host.Length == 0)
            {
                ShowWarn("请填写远程主机地址。");
                txtHost.Focus();
                return;
            }

            _connecting = true;
            btnConnect.Enabled = false;
            SetState("… 正在连接 " + host + ":" + port, Theme.Warning);

            Task.Factory.StartNew(delegate
            {
                string error = null;
                try
                {
                    _client.Connect(host, port, ConnectTimeoutMs);
                }
                catch (Exception ex)
                {
                    error = ex.Message;
                }

                SafeUi(delegate
                {
                    _connecting = false;
                    btnConnect.Enabled = true;
                    if (error != null)
                    {
                        SetState("○ 未连接", Theme.TextMuted);
                        SetHint("连接失败：" + error);
                        ShowWarn("连接 " + host + ":" + port + " 失败：\r\n" + error);
                    }
                    UpdateModeUi();
                });
            });
        }

        private void Server_Started(object sender, TcpListeningEventArgs e)
        {
            SafeUi(delegate
            {
                SetState(string.Format("● 监听中 0.0.0.0:{0}", e.Port), Theme.Success);
                UpdateModeUi();
            });
        }

        private void Server_Stopped(object sender, TcpListeningEventArgs e)
        {
            SafeUi(delegate
            {
                lstClients.Items.Clear();
                _serverDecoders.Clear();
                SetState("○ 已停止监听", Theme.TextMuted);
                UpdateModeUi();
                UpdateClientCount();
            });
        }

        private void Server_ClientConnected(object sender, ClientEventArgs e)
        {
            SafeUi(delegate
            {
                lstClients.Items.Add(e.Client);
                _serverDecoders.Remove(e.Client.Id);
                UpdateClientCount();
                SetState(string.Format("● 监听中 · 新连接 {0}", e.Client), Theme.Success);
                SetHint(string.Format("客户端 {0} 已接入", e.Client));
            });
        }

        private void Server_ClientDisconnected(object sender, ClientEventArgs e)
        {
            SafeUi(delegate
            {
                for (int i = lstClients.Items.Count - 1; i >= 0; i--)
                {
                    ClientInfo info = lstClients.Items[i] as ClientInfo;
                    if (info != null && info.Id == e.Client.Id) lstClients.Items.RemoveAt(i);
                }
                _serverDecoders.Remove(e.Client.Id);
                UpdateClientCount();
                if (_server.IsRunning)
                {
                    SetState(string.Format("● 监听中 · {0} 已断开", e.Client), Theme.Warning);
                    SetHint(string.Format("客户端 {0} 已断开", e.Client));
                }
            });
        }

        private void Client_Connected(object sender, TcpConnectedEventArgs e)
        {
            SafeUi(delegate
            {
                _clientDecoder.SetEncoding(cboEncoding.Text);
                SetState(string.Format("● 已连接 {0}", e.RemoteAddress), Theme.Success);
                SetHint(string.Format("本地 {0} → 远端 {1}", e.LocalAddress, e.RemoteAddress));
                UpdateModeUi();
            });
        }

        private void Client_Closed(object sender, EventArgs e)
        {
            SafeUi(delegate
            {
                SetState("○ 已断开连接", Theme.TextMuted);
                UpdateModeUi();
            });
        }

        private void Client_Failed(object sender, TcpErrorEventArgs e)
        {
            SafeUi(delegate { SetHint("⚠ " + e.Message); });
        }

        private void Server_Failed(object sender, TcpErrorEventArgs e)
        {
            SafeUi(delegate { SetHint("⚠ " + e.Message); });
        }

        // ===================== 接收 =====================

        private void Server_DataReceived(object sender, TcpDataEventArgs e)
        {
            HandleIncoming(e, true);
        }

        private void Client_DataReceived(object sender, TcpDataEventArgs e)
        {
            HandleIncoming(e, false);
        }

        private void HandleIncoming(TcpDataEventArgs e, bool serverMode)
        {
            SafeUi(delegate
            {
                _stats.AddReceived(e.Data.Length);
                UpdateStatsUi();
                if (chkPause.Checked) return;
                EnqueueRecv(PacketSegments(e, true, serverMode));
            });
        }

        /// <summary>带颜色的一段日志文本（时间戳灰度、方向标记彩色、正文深色）。</summary>
        private struct LogSegment
        {
            public readonly string Text;
            public readonly Color Color;

            public LogSegment(string text, Color color)
            {
                Text = text;
                Color = color;
            }
        }

        /// <summary>把一个数据包拆成若干带颜色的片段。</summary>
        private List<LogSegment> PacketSegments(TcpDataEventArgs e, bool received, bool serverMode)
        {
            List<LogSegment> segments = new List<LogSegment>(6);
            Color bodyColor = received ? Theme.LogReceived : Theme.LogSent;

            if (chkTimestamp.Checked)
            {
                segments.Add(new LogSegment("[" + e.Timestamp.ToString("HH:mm:ss.fff") + "] ", Theme.LogTime));
            }

            segments.Add(new LogSegment(received ? "← " : "→ ", received ? Theme.LogWho : Theme.LogSent));

            if (serverMode && e.ClientId > 0)
            {
                segments.Add(new LogSegment("#" + e.ClientId + " ", Theme.LogWho));
            }

            if (chkShowHex.Checked)
            {
                segments.Add(new LogSegment(HexUtil.Format(e.Data), bodyColor));
                segments.Add(new LogSegment("\r\n", bodyColor));
                return segments;
            }

            string text = received
                ? DecodeIncoming(e, serverMode)
                : TextCodec.Decode(e.Data, 0, e.Data.Length, cboEncoding.Text);
            text = TextCodec.EscapeControlChars(text);
            if (text.Length == 0 || text[text.Length - 1] != '\n') text += "\r\n";
            segments.Add(new LogSegment(text, bodyColor));
            return segments;
        }

        private string DecodeIncoming(TcpDataEventArgs e, bool serverMode)
        {
            if (!serverMode) return _clientDecoder.Decode(e.Data, 0, e.Data.Length);

            StreamTextDecoder decoder;
            if (!_serverDecoders.TryGetValue(e.ClientId, out decoder))
            {
                decoder = new StreamTextDecoder(cboEncoding.Text);
                _serverDecoders[e.ClientId] = decoder;
            }
            return decoder.Decode(e.Data, 0, e.Data.Length);
        }

        private void EnqueueRecv(List<LogSegment> segments)
        {
            if (segments == null || segments.Count == 0) return;
            _recvPending.AddRange(segments);
        }

        /// <summary>定时把缓冲的内容刷到界面上，避免高频收包把 UI 卡死。</summary>
        private void FlushRecvPending(object sender, EventArgs e)
        {
            if (_recvPending.Count == 0) return;

            List<LogSegment> items = new List<LogSegment>(_recvPending);
            _recvPending.Clear();

            if (txtRecv.TextLength > MaxRecvChars)
            {
                txtRecv.Clear();
                AppendSegment(new LogSegment("（接收区内容过长，已自动清空，建议及时保存日志）\r\n", Theme.LogTime));
            }

            foreach (LogSegment segment in items)
            {
                AppendSegment(segment);
            }

            if (chkAutoScroll.Checked)
            {
                txtRecv.SelectionStart = txtRecv.TextLength;
                txtRecv.SelectionLength = 0;
                txtRecv.ScrollToCaret();
            }
        }

        private void AppendSegment(LogSegment segment)
        {
            if (segment.Text == null || segment.Text.Length == 0) return;
            txtRecv.SelectionStart = txtRecv.TextLength;
            txtRecv.SelectionLength = 0;
            txtRecv.SelectionColor = segment.Color;
            txtRecv.AppendText(segment.Text);
        }

        private void btnClearRecv_Click(object sender, EventArgs e)
        {
            _recvPending.Clear();
            txtRecv.Clear();
            SetHint("接收区已清空");
        }

        private void btnSaveLog_Click(object sender, EventArgs e)
        {
            SaveFileDialog dialog = new SaveFileDialog();
            try
            {
                dialog.Filter = "文本文件 (*.txt)|*.txt|所有文件 (*.*)|*.*";
                dialog.FileName = "tcp-log-" + DateTime.Now.ToString("yyyyMMdd-HHmmss") + ".txt";
                if (dialog.ShowDialog(this) != DialogResult.OK) return;
                File.WriteAllText(dialog.FileName, txtRecv.Text, new UTF8Encoding(true));
                SetHint("日志已保存：" + dialog.FileName);
            }
            catch (Exception ex)
            {
                ShowWarn("保存日志失败：" + ex.Message);
            }
            finally
            {
                dialog.Dispose();
            }
        }

        private void chkPause_CheckedChanged(object sender, EventArgs e)
        {
            SetHint(chkPause.Checked ? "已暂停显示（数据仍在计数）" : "已恢复显示");
        }

        private void chkAutoScroll_CheckedChanged(object sender, EventArgs e)
        {
            if (chkAutoScroll.Checked)
            {
                txtRecv.SelectionStart = txtRecv.TextLength;
                txtRecv.ScrollToCaret();
            }
        }

        private void chkShowHex_CheckedChanged(object sender, EventArgs e)
        {
            ResetDecoders();
            SetHint(chkShowHex.Checked ? "接收区按十六进制显示" : "接收区按文本显示");
        }

        // ===================== 发送 =====================

        private void btnSend_Click(object sender, EventArgs e)
        {
            SendCurrent(false);
        }

        private void txtSend_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Control && (e.KeyCode == Keys.Enter || e.KeyCode == Keys.Return))
            {
                e.SuppressKeyPress = true;
                SendCurrent(false);
            }
        }

        private void SendCurrent(bool auto)
        {
            byte[] payload;
            string error;
            if (!TryBuildPayload(out payload, out error))
            {
                if (auto) StopAutoSend(error);
                else ShowWarn(error);
                return;
            }
            if (payload.Length == 0)
            {
                if (auto) StopAutoSend("发送内容为空");
                else ShowWarn("发送内容为空。");
                return;
            }

            int sent;
            if (_server.IsRunning)
            {
                if (chkBroadcast.Checked)
                {
                    sent = _server.Broadcast(payload);
                }
                else
                {
                    List<long> ids = SelectedClientIds();
                    if (ids.Count == 0)
                    {
                        if (auto) StopAutoSend("没有选中客户端");
                        else ShowWarn("请先在左侧选择一个客户端，或勾选“广播给所有客户端”。");
                        return;
                    }
                    sent = _server.SendTo(ids, payload);
                }
                if (sent == 0)
                {
                    if (auto) StopAutoSend("没有可用的客户端");
                    else ShowWarn("没有可用的客户端连接。");
                    return;
                }
            }
            else
            {
                if (!_client.IsConnected)
                {
                    if (auto) StopAutoSend("尚未连接");
                    else ShowWarn("尚未连接到服务器，请先点击“连接”。");
                    return;
                }
                if (!_client.Send(payload))
                {
                    if (auto) StopAutoSend("发送失败");
                    else ShowWarn("发送失败，请检查连接状态。");
                    return;
                }
                sent = 1;
            }

            _stats.AddSent(payload.Length * sent);
            UpdateStatsUi();

            if (chkEchoSent.Checked)
            {
                TcpDataEventArgs echo = new TcpDataEventArgs(0, payload);
                EnqueueRecv(PacketSegments(echo, false, false));
            }

            if (!auto) AddHistory(txtSend.Text);
            SetHint(string.Format("已发送 {0} 字节，共 {1} 个连接", payload.Length, sent));
        }

        private bool TryBuildPayload(out byte[] payload, out string error)
        {
            payload = null;
            error = null;
            string raw = txtSend.Text;
            string newLine = CurrentNewLine();

            if (chkSendHex.Checked)
            {
                if (!HexUtil.TryParse(raw, out payload, out error))
                {
                    error = "十六进制内容有误：" + error;
                    return false;
                }
                if (newLine.Length > 0)
                {
                    byte[] nlBytes = Encoding.ASCII.GetBytes(newLine);
                    byte[] merged = new byte[payload.Length + nlBytes.Length];
                    Buffer.BlockCopy(payload, 0, merged, 0, payload.Length);
                    Buffer.BlockCopy(nlBytes, 0, merged, payload.Length, nlBytes.Length);
                    payload = merged;
                }
                return true;
            }

            payload = TextCodec.Encode(raw + newLine, cboEncoding.Text);
            return true;
        }

        private string CurrentNewLine()
        {
            switch (cboNewLine.SelectedIndex)
            {
                case 1: return "\n";
                case 2: return "\r\n";
                default: return string.Empty;
            }
        }

        private List<long> SelectedClientIds()
        {
            List<long> ids = new List<long>();
            foreach (object item in lstClients.SelectedItems)
            {
                ClientInfo info = item as ClientInfo;
                if (info != null) ids.Add(info.Id);
            }
            return ids;
        }

        private void btnClearSend_Click(object sender, EventArgs e)
        {
            txtSend.Clear();
            txtSend.Focus();
        }

        private void chkSendHex_CheckedChanged(object sender, EventArgs e)
        {
            if (!chkSendHex.Checked)
            {
                SetHint("文本发送");
                return;
            }

            byte[] probe;
            string error;
            if (HexUtil.TryParse(txtSend.Text, out probe, out error))
            {
                string normalized;
                if (HexUtil.TryNormalize(txtSend.Text, out normalized, out error) && normalized.Length > 0)
                {
                    txtSend.Text = normalized;
                }
                SetHint("十六进制发送：用空格分隔字节，例如 01 0A FF");
            }
            else
            {
                SetHint("十六进制发送已开启，但当前内容不是合法的十六进制");
            }
        }

        private void cboHistory_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (_suppressHistoryEvent) return;
            string item = cboHistory.SelectedItem as string;
            if (item == null) return;
            txtSend.Text = item;
            txtSend.SelectionStart = txtSend.TextLength;
            txtSend.Focus();
        }

        private void AddHistory(string text)
        {
            if (string.IsNullOrEmpty(text)) return;

            _history.Remove(text);
            _history.Insert(0, text);
            while (_history.Count > HistoryLimit) _history.RemoveAt(_history.Count - 1);

            _suppressHistoryEvent = true;
            try
            {
                cboHistory.Items.Clear();
                cboHistory.Items.AddRange(_history.ToArray());
                if (cboHistory.Items.Count > 0) cboHistory.SelectedIndex = 0;
            }
            finally
            {
                _suppressHistoryEvent = false;
            }
        }

        // ===================== 定时发送 =====================

        private void AutoSendTimer_Tick(object sender, EventArgs e)
        {
            _autoSendTimer.Stop();
            try
            {
                SendCurrent(true);
            }
            finally
            {
                if (chkAutoSend.Checked) _autoSendTimer.Start();
            }
        }

        private void chkAutoSend_CheckedChanged(object sender, EventArgs e)
        {
            if (chkAutoSend.Checked)
            {
                if (!IsReadyToSend())
                {
                    chkAutoSend.Checked = false;
                    SetHint("定时发送需要先建立连接");
                    return;
                }
                _autoSendTimer.Interval = (int)numInterval.Value;
                _autoSendTimer.Start();
                SetHint(string.Format("定时发送已开启：每 {0} 毫秒一次", (int)numInterval.Value));
            }
            else
            {
                _autoSendTimer.Stop();
                SetHint("定时发送已关闭");
            }
        }

        private void numInterval_ValueChanged(object sender, EventArgs e)
        {
            if (_autoSendTimer == null) return;
            if (chkAutoSend.Checked) _autoSendTimer.Interval = (int)numInterval.Value;
        }

        private void StopAutoSend(string reason)
        {
            _autoSendTimer.Stop();
            if (chkAutoSend.Checked) chkAutoSend.Checked = false;
            SetHint("定时发送已停止：" + reason);
        }

        private bool IsReadyToSend()
        {
            if (_server.IsRunning)
            {
                if (chkBroadcast.Checked) return _server.ClientCount > 0;
                return SelectedClientIds().Count > 0;
            }
            return _client.IsConnected;
        }

        // ===================== 模式与编码 =====================

        private void Mode_CheckedChanged(object sender, EventArgs e)
        {
            RadioButton radio = sender as RadioButton;
            if (radio == null || !radio.Checked) return;

            if (rbServer.Checked)
            {
                _savedHost = txtHost.Text.Trim();
                txtHost.Text = "0.0.0.0";
            }
            else
            {
                txtHost.Text = string.IsNullOrEmpty(_savedHost) ? "127.0.0.1" : _savedHost;
            }
            UpdateModeUi();
        }

        private void cboEncoding_SelectedIndexChanged(object sender, EventArgs e)
        {
            ResetDecoders();
            SetHint("编码已切换为 " + cboEncoding.Text);
        }

        private void chkBroadcast_CheckedChanged(object sender, EventArgs e)
        {
            if (chkBroadcast.Checked) lstClients.ClearSelected();
            UpdateModeUi();
        }

        private void ResetDecoders()
        {
            _clientDecoder.SetEncoding(cboEncoding.Text);
            _serverDecoders.Clear();
        }

        // ===================== 界面状态 =====================

        private void UpdateModeUi()
        {
            bool serverMode = rbServer.Checked;
            bool busy = _server.IsRunning || _client.IsConnected;

            rbClient.Enabled = !busy;
            rbServer.Enabled = !busy;
            lblHost.Text = serverMode ? "本地地址" : "远程主机";
            txtHost.Enabled = !serverMode && !busy;
            numPort.Enabled = !busy;

            if (serverMode) txtHost.Text = "0.0.0.0";

            // 连接中/监听中按钮变成「危险色」，一眼能看出当前该点是断开
            if (_server.IsRunning)
            {
                btnConnect.Text = "停止监听";
                btnConnect.ButtonKind = FlatButton.Kind.Danger;
            }
            else if (_client.IsConnected)
            {
                btnConnect.Text = "断开连接";
                btnConnect.ButtonKind = FlatButton.Kind.Danger;
            }
            else
            {
                btnConnect.Text = serverMode ? "开始监听" : "连接";
                btnConnect.ButtonKind = FlatButton.Kind.Primary;
            }

            lstClients.Enabled = serverMode && !chkBroadcast.Checked;
            chkBroadcast.Enabled = serverMode;
            UpdateClientCount();
        }

        private void UpdateClientCount()
        {
            int count = lstClients.Items.Count;
            lblClientCount.Text = string.Format("共 {0} 个客户端（按住 Ctrl 可多选）", count);
            lblClients.Text = "客户端 " + count;
        }

        private void UpdateStatsUi()
        {
            lblRecvBytes.Text = "↓ " + TextCodec.FormatBytes(_stats.ReceivedBytes);
            lblSentBytes.Text = "↑ " + TextCodec.FormatBytes(_stats.SentBytes);
        }

        private void SetState(string text, Color color)
        {
            // 状态文字里自带的 ●/○ 交给胶囊的圆点表示，文本去掉前缀
            string cleaned = (text ?? string.Empty).TrimStart('●', '○', ' ');
            statusPill.SetState(cleaned, color);
        }

        private void SetHint(string text)
        {
            lblHint.Text = text;
        }

        private void ShowWarn(string text)
        {
            MessageBox.Show(this, text, "TCP 调试助手", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }

        /// <summary>把一段操作切回 UI 线程执行（后台线程的事件都用它）。</summary>
        private void SafeUi(MethodInvoker action)
        {
            if (action == null) return;
            if (IsDisposed || Disposing) return;
            if (!IsHandleCreated) return;

            if (InvokeRequired)
            {
                try { BeginInvoke(action); }
                catch (InvalidOperationException) { }   // 含窗口已销毁（ObjectDisposedException）的情况
            }
            else
            {
                action();
            }
        }

        // ===================== 设置持久化 =====================

        private static bool TryGet(Dictionary<string, string> values, string key, out string value)
        {
            return values.TryGetValue(key, out value);
        }

        private static bool GetBool(Dictionary<string, string> values, string key, bool fallback)
        {
            string raw;
            if (!values.TryGetValue(key, out raw)) return fallback;
            if (raw == "1") return true;
            if (raw == "0") return false;
            return string.Equals(raw, "true", StringComparison.OrdinalIgnoreCase);
        }

        private void ApplySettings(Dictionary<string, string> values)
        {
            string value;

            if (TryGet(values, "mode", out value) && string.Equals(value, "server", StringComparison.OrdinalIgnoreCase))
                rbServer.Checked = true;
            else
                rbClient.Checked = true;

            if (TryGet(values, "host", out value) && value.Trim().Length > 0)
            {
                _savedHost = value.Trim();
                if (rbClient.Checked) txtHost.Text = _savedHost;
            }
            if (TryGet(values, "port", out value))
            {
                int port;
                if (int.TryParse(value, out port) && port >= 1 && port <= 65535) numPort.Value = port;
            }
            if (TryGet(values, "encoding", out value))
            {
                int index = cboEncoding.Items.IndexOf(value);
                if (index >= 0) cboEncoding.SelectedIndex = index;
            }
            if (TryGet(values, "newline", out value))
            {
                int index;
                if (int.TryParse(value, out index) && index >= 0 && index < cboNewLine.Items.Count)
                    cboNewLine.SelectedIndex = index;
            }
            if (TryGet(values, "interval", out value))
            {
                int interval;
                if (int.TryParse(value, out interval) && interval >= numInterval.Minimum && interval <= numInterval.Maximum)
                    numInterval.Value = interval;
            }

            chkShowHex.Checked = GetBool(values, "showHex", chkShowHex.Checked);
            chkTimestamp.Checked = GetBool(values, "timestamp", chkTimestamp.Checked);
            chkEchoSent.Checked = GetBool(values, "echoSent", chkEchoSent.Checked);
            chkSendHex.Checked = GetBool(values, "sendHex", chkSendHex.Checked);
            chkAutoScroll.Checked = GetBool(values, "autoScroll", chkAutoScroll.Checked);
            chkBroadcast.Checked = GetBool(values, "broadcast", chkBroadcast.Checked);

            if (rbServer.Checked) txtHost.Text = "0.0.0.0";
        }

        private void SaveSettings()
        {
            Dictionary<string, string> values = new Dictionary<string, string>();
            values["mode"] = rbServer.Checked ? "server" : "client";
            values["host"] = rbServer.Checked ? _savedHost : txtHost.Text.Trim();
            values["port"] = ((int)numPort.Value).ToString();
            values["encoding"] = cboEncoding.Text;
            values["newline"] = cboNewLine.SelectedIndex.ToString();
            values["interval"] = ((int)numInterval.Value).ToString();
            values["showHex"] = chkShowHex.Checked ? "1" : "0";
            values["timestamp"] = chkTimestamp.Checked ? "1" : "0";
            values["echoSent"] = chkEchoSent.Checked ? "1" : "0";
            values["sendHex"] = chkSendHex.Checked ? "1" : "0";
            values["autoScroll"] = chkAutoScroll.Checked ? "1" : "0";
            values["broadcast"] = chkBroadcast.Checked ? "1" : "0";
            SettingsStore.Save(values);
        }

        private void MainForm_FormClosing(object sender, FormClosingEventArgs e)
        {
            SaveSettings();

            if (_flushTimer != null) _flushTimer.Stop();
            if (_autoSendTimer != null) _autoSendTimer.Stop();

            try { _client.Close(); }
            catch (Exception) { }

            try
            {
                if (_server.IsRunning) _server.Stop();
            }
            catch (Exception) { }
        }
    }
}
