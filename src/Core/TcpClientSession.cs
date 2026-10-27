using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Threading;

namespace TcpTool.Core
{
    /// <summary>
    /// TCP 客户端会话：连接远端、收发数据。
    /// 事件都在后台线程触发，界面层需要自行切回 UI 线程。
    /// </summary>
    public sealed class TcpClientSession : IDisposable
    {
        private const int BufferSize = 16 * 1024;

        private readonly object _sync = new object();
        private TcpClient _tcp;
        private volatile bool _connected;
        private bool _disposed;

        /// <summary>连接成功。</summary>
        public event EventHandler<TcpConnectedEventArgs> Connected;

        /// <summary>连接已断开（包括主动断开）。</summary>
        public event EventHandler<EventArgs> Closed;

        public event EventHandler<TcpDataEventArgs> DataReceived;
        public event EventHandler<TcpErrorEventArgs> Failed;

        public bool IsConnected { get { return _connected; } }

        /// <summary>本地端点，例如 "127.0.0.1:51234"；未连接时为 "-"。</summary>
        public string LocalEndPoint { get; private set; }

        /// <summary>远端端点，例如 "127.0.0.1:8000"；未连接时为 "-"。</summary>
        public string RemoteEndPoint { get; private set; }

        /// <summary>连接远端。超时或失败时抛出异常，调用方负责提示。</summary>
        public void Connect(string host, int port, int timeoutMs)
        {
            if (_disposed) throw new ObjectDisposedException("TcpClientSession");
            if (string.IsNullOrEmpty(host)) throw new ArgumentNullException("host", "远程主机不能为空。");
            if (port < 1 || port > 65535) throw new ArgumentOutOfRangeException("port", "端口号必须在 1 ~ 65535 之间。");

            Close();

            TcpClient tcp = new TcpClient();
            try
            {
                tcp.NoDelay = true;
                IAsyncResult ar = tcp.BeginConnect(host, port, null, null);
                bool finished = ar.AsyncWaitHandle.WaitOne(timeoutMs < 1 ? 5000 : timeoutMs);
                if (!finished)
                {
                    tcp.Close();
                    throw new TimeoutException(string.Format("连接 {0}:{1} 超时（{2} 毫秒）。", host, port, timeoutMs));
                }
                tcp.EndConnect(ar);
            }
            catch (SocketException ex)
            {
                try { tcp.Close(); }
                catch (SocketException) { }
                throw new IOException(string.Format("无法连接到 {0}:{1} —— {2}", host, port, ex.Message), ex);
            }
            catch (Exception)
            {
                try { tcp.Close(); }
                catch (SocketException) { }
                throw;
            }

            IPEndPoint local = tcp.Client.LocalEndPoint as IPEndPoint;
            IPEndPoint remote = tcp.Client.RemoteEndPoint as IPEndPoint;
            LocalEndPoint = local != null ? local.Address + ":" + local.Port : "-";
            RemoteEndPoint = remote != null ? remote.Address + ":" + remote.Port : "-";

            lock (_sync)
            {
                _tcp = tcp;
                _connected = true;
            }

            EventHandler<TcpConnectedEventArgs> handler = Connected;
            if (handler != null) handler(this, new TcpConnectedEventArgs(local, remote));

            Thread thread = new Thread(ReadLoop);
            thread.IsBackground = true;
            thread.Name = "tcp-client-read";
            thread.Start(tcp);
        }

        /// <summary>发送数据，返回是否成功。</summary>
        public bool Send(byte[] data)
        {
            if (data == null || data.Length == 0) return false;

            TcpClient tcp;
            lock (_sync) { tcp = _tcp; }
            if (tcp == null || !_connected) return false;

            try
            {
                NetworkStream stream = tcp.GetStream();
                stream.Write(data, 0, data.Length);
                stream.Flush();
                return true;
            }
            catch (IOException ex)
            {
                RaiseFailed("发送失败：" + ex.Message, ex);
                return false;
            }
            catch (SocketException ex)
            {
                RaiseFailed("发送失败：" + ex.Message, ex);
                return false;
            }
            catch (ObjectDisposedException ex)
            {
                RaiseFailed("发送失败：连接已关闭。", ex);
                return false;
            }
        }

        private void ReadLoop(object state)
        {
            TcpClient tcp = (TcpClient)state;
            byte[] buffer = new byte[BufferSize];

            try
            {
                NetworkStream stream = tcp.GetStream();
                while (true)
                {
                    int read = stream.Read(buffer, 0, buffer.Length);
                    if (read <= 0) break;

                    byte[] data = new byte[read];
                    Buffer.BlockCopy(buffer, 0, data, 0, read);

                    EventHandler<TcpDataEventArgs> handler = DataReceived;
                    if (handler != null) handler(this, new TcpDataEventArgs(data));
                }
            }
            catch (IOException) { }
            catch (SocketException) { }
            catch (ObjectDisposedException) { }

            bool notify;
            lock (_sync)
            {
                notify = _connected && ReferenceEquals(_tcp, tcp);
                if (ReferenceEquals(_tcp, tcp)) _tcp = null;
                _connected = false;
            }

            try { tcp.Close(); }
            catch (SocketException) { }
            catch (ObjectDisposedException) { }

            if (notify)
            {
                EventHandler<EventArgs> handler = Closed;
                if (handler != null) handler(this, EventArgs.Empty);
            }
        }

        /// <summary>主动断开连接。</summary>
        public void Close()
        {
            TcpClient tcp;
            bool wasConnected;
            lock (_sync)
            {
                tcp = _tcp;
                wasConnected = _connected;
                _tcp = null;
                _connected = false;
            }

            if (tcp != null)
            {
                try { tcp.Close(); }
                catch (SocketException) { }
                catch (ObjectDisposedException) { }
            }

            if (wasConnected)
            {
                EventHandler<EventArgs> handler = Closed;
                if (handler != null) handler(this, EventArgs.Empty);
            }
        }

        private void RaiseFailed(string message, Exception ex)
        {
            EventHandler<TcpErrorEventArgs> handler = Failed;
            if (handler != null) handler(this, new TcpErrorEventArgs(message, ex));
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            Close();
        }
    }
}
