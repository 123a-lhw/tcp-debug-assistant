using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Threading;

namespace TcpTool.Core
{
    /// <summary>
    /// TCP 服务端：监听端口、管理多个客户端连接、按客户端定向发送或广播。
    /// 所有事件都在后台线程触发，界面层需要自行切回 UI 线程。
    /// </summary>
    public sealed class TcpServer : IDisposable
    {
        private sealed class ClientEntry
        {
            public readonly TcpClient Tcp;
            public readonly ClientInfo Info;
            public readonly object WriteLock = new object();
            private int _disconnectNotified;

            public ClientEntry(TcpClient tcp, ClientInfo info)
            {
                Tcp = tcp;
                Info = info;
            }

            /// <summary>返回 true 表示本次调用应当触发“断开”事件（保证只触发一次）。</summary>
            public bool TryNotifyDisconnected()
            {
                return Interlocked.Exchange(ref _disconnectNotified, 1) == 0;
            }

            public void SuppressDisconnectNotification()
            {
                Interlocked.Exchange(ref _disconnectNotified, 1);
            }
        }

        private const int BufferSize = 16 * 1024;
        private const int Backlog = 64;

        private readonly object _sync = new object();
        private readonly Dictionary<long, ClientEntry> _clients = new Dictionary<long, ClientEntry>();
        private TcpListener _listener;
        private Thread _acceptThread;
        private long _nextClientId = 1;
        private volatile bool _running;
        private bool _disposed;

        /// <summary>开始监听成功（端口可能由系统分配，以事件里的 Port 为准）。</summary>
        public event EventHandler<TcpListeningEventArgs> Started;

        /// <summary>已停止监听。</summary>
        public event EventHandler<TcpListeningEventArgs> Stopped;

        public event EventHandler<ClientEventArgs> ClientConnected;
        public event EventHandler<ClientEventArgs> ClientDisconnected;
        public event EventHandler<TcpDataEventArgs> DataReceived;
        public event EventHandler<TcpErrorEventArgs> Failed;

        public bool IsRunning { get { return _running; } }

        /// <summary>实际监听的端口。</summary>
        public int Port { get; private set; }

        public int ClientCount
        {
            get { lock (_sync) { return _clients.Count; } }
        }

        /// <summary>当前所有客户端的快照。</summary>
        public ClientInfo[] GetClients()
        {
            lock (_sync)
            {
                ClientInfo[] result = new ClientInfo[_clients.Count];
                int i = 0;
                foreach (ClientEntry entry in _clients.Values)
                {
                    result[i++] = entry.Info;
                }
                return result;
            }
        }

        /// <summary>开始监听。端口传 0 表示由系统分配。</summary>
        public void Start(int port)
        {
            if (_disposed) throw new ObjectDisposedException("TcpServer");
            if (port < 0 || port > 65535)
                throw new ArgumentOutOfRangeException("port", "端口号必须在 0 ~ 65535 之间。");

            lock (_sync)
            {
                if (_running) throw new InvalidOperationException("服务端已经在监听状态了。");

                TcpListener listener = new TcpListener(IPAddress.Any, port);
                listener.Start(Backlog);

                _listener = listener;
                Port = ((IPEndPoint)listener.LocalEndpoint).Port;
                _running = true;

                _acceptThread = new Thread(AcceptLoop);
                _acceptThread.IsBackground = true;
                _acceptThread.Name = "tcp-accept";
                _acceptThread.Start();
            }

            EventHandler<TcpListeningEventArgs> handler = Started;
            if (handler != null) handler(this, new TcpListeningEventArgs(Port, "0.0.0.0"));
        }

        /// <summary>停止监听并断开所有客户端。</summary>
        public void Stop()
        {
            TcpListener listener;
            Thread acceptThread;
            List<ClientEntry> closing;

            lock (_sync)
            {
                if (!_running && _listener == null) return;

                _running = false;
                listener = _listener;
                _listener = null;
                acceptThread = _acceptThread;
                _acceptThread = null;

                closing = new List<ClientEntry>(_clients.Values);
                _clients.Clear();
                foreach (ClientEntry entry in closing)
                {
                    // 主动停止时不再逐个上报“客户端断开”，界面统一按“停止监听”处理
                    entry.SuppressDisconnectNotification();
                }
            }

            if (listener != null)
            {
                try { listener.Stop(); }
                catch (SocketException) { }
                catch (ObjectDisposedException) { }
            }

            foreach (ClientEntry entry in closing)
            {
                try { entry.Tcp.Close(); }
                catch (SocketException) { }
                catch (ObjectDisposedException) { }
            }

            if (acceptThread != null && acceptThread != Thread.CurrentThread)
            {
                try { acceptThread.Join(1000); }
                catch (ThreadStateException) { }
            }

            EventHandler<TcpListeningEventArgs> handler = Stopped;
            if (handler != null) handler(this, new TcpListeningEventArgs(Port, "0.0.0.0"));
        }

        /// <summary>发送给指定客户端，返回实际成功发送的客户端数量（0 或 1）。</summary>
        public int SendTo(long clientId, byte[] data)
        {
            return SendCore(data, new long[] { clientId });
        }

        /// <summary>发送给指定的一组客户端。</summary>
        public int SendTo(IEnumerable<long> clientIds, byte[] data)
        {
            if (clientIds == null) return 0;
            return SendCore(data, new List<long>(clientIds));
        }

        /// <summary>广播给所有已连接客户端，返回成功发送的数量。</summary>
        public int Broadcast(byte[] data)
        {
            return SendCore(data, null);
        }

        private int SendCore(byte[] data, IList<long> targetIds)
        {
            if (data == null || data.Length == 0) return 0;

            List<ClientEntry> targets = new List<ClientEntry>();
            lock (_sync)
            {
                if (targetIds == null)
                {
                    targets.AddRange(_clients.Values);
                }
                else
                {
                    foreach (long id in targetIds)
                    {
                        ClientEntry entry;
                        if (_clients.TryGetValue(id, out entry)) targets.Add(entry);
                    }
                }
            }

            int sent = 0;
            foreach (ClientEntry entry in targets)
            {
                lock (entry.WriteLock)
                {
                    try
                    {
                        NetworkStream stream = entry.Tcp.GetStream();
                        stream.Write(data, 0, data.Length);
                        stream.Flush();
                        entry.Info.AddSent(data.Length);
                        sent++;
                    }
                    catch (IOException ex)
                    {
                        RaiseFailed(string.Format("向客户端 {0} 发送失败：{1}", entry.Info, ex.Message), ex);
                    }
                    catch (SocketException ex)
                    {
                        RaiseFailed(string.Format("向客户端 {0} 发送失败：{1}", entry.Info, ex.Message), ex);
                    }
                    catch (ObjectDisposedException ex)
                    {
                        RaiseFailed(string.Format("向客户端 {0} 发送失败：连接已关闭。", entry.Info), ex);
                    }
                }
            }
            return sent;
        }

        private void AcceptLoop()
        {
            TcpListener listener = _listener;
            if (listener == null) return;

            while (true)
            {
                TcpClient client;
                try
                {
                    client = listener.AcceptTcpClient();
                }
                catch (ObjectDisposedException)
                {
                    break;
                }
                catch (SocketException ex)
                {
                    if (!_running) break;
                    RaiseFailed("接受客户端连接失败：" + ex.Message, ex);
                    break;
                }
                catch (InvalidOperationException)
                {
                    break;
                }

                if (!_running)
                {
                    try { client.Close(); }
                    catch (SocketException) { }
                    break;
                }

                RegisterClient(client);
            }
        }

        private void RegisterClient(TcpClient tcp)
        {
            long id;
            ClientEntry entry;
            try
            {
                tcp.NoDelay = true;
            }
            catch (SocketException) { }
            catch (ObjectDisposedException) { return; }

            lock (_sync)
            {
                id = _nextClientId++;
                IPEndPoint remote = tcp.Client.RemoteEndPoint as IPEndPoint;
                entry = new ClientEntry(tcp, new ClientInfo(id, remote));
                _clients[id] = entry;
            }

            Thread thread = new Thread(ReadLoop);
            thread.IsBackground = true;
            thread.Name = "tcp-client-" + id;
            thread.Start(entry);

            EventHandler<ClientEventArgs> handler = ClientConnected;
            if (handler != null) handler(this, new ClientEventArgs(entry.Info));
        }

        private void ReadLoop(object state)
        {
            ClientEntry entry = (ClientEntry)state;
            byte[] buffer = new byte[BufferSize];

            try
            {
                NetworkStream stream = entry.Tcp.GetStream();
                while (true)
                {
                    int read = stream.Read(buffer, 0, buffer.Length);
                    if (read <= 0) break;

                    byte[] data = new byte[read];
                    Buffer.BlockCopy(buffer, 0, data, 0, read);
                    entry.Info.AddReceived(read);

                    EventHandler<TcpDataEventArgs> handler = DataReceived;
                    if (handler != null) handler(this, new TcpDataEventArgs(entry.Info.Id, data));
                }
            }
            catch (IOException) { }
            catch (SocketException) { }
            catch (ObjectDisposedException) { }

            lock (_sync)
            {
                ClientEntry current;
                if (_clients.TryGetValue(entry.Info.Id, out current) && ReferenceEquals(current, entry))
                    _clients.Remove(entry.Info.Id);
            }

            try { entry.Tcp.Close(); }
            catch (SocketException) { }
            catch (ObjectDisposedException) { }

            if (entry.TryNotifyDisconnected())
            {
                EventHandler<ClientEventArgs> handler = ClientDisconnected;
                if (handler != null) handler(this, new ClientEventArgs(entry.Info));
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
            try { Stop(); }
            catch (Exception) { }
        }
    }
}
