using System;
using System.Net;
using System.Threading;

namespace TcpTool.Core
{
    /// <summary>一个已连接客户端的信息快照。</summary>
    public sealed class ClientInfo
    {
        private long _sentBytes;
        private long _receivedBytes;

        public ClientInfo(long id, IPEndPoint remoteEndPoint)
        {
            Id = id;
            ConnectedAt = DateTime.Now;
            if (remoteEndPoint != null)
            {
                Address = remoteEndPoint.Address.ToString();
                Port = remoteEndPoint.Port;
            }
            else
            {
                Address = "unknown";
                Port = 0;
            }
        }

        public long Id { get; private set; }
        public string Address { get; private set; }
        public int Port { get; private set; }
        public DateTime ConnectedAt { get; private set; }

        public long SentBytes { get { return Interlocked.Read(ref _sentBytes); } }
        public long ReceivedBytes { get { return Interlocked.Read(ref _receivedBytes); } }

        public void AddSent(int count) { Interlocked.Add(ref _sentBytes, count); }
        public void AddReceived(int count) { Interlocked.Add(ref _receivedBytes, count); }

        public override string ToString()
        {
            return string.Format("#{0}  {1}:{2}", Id, Address, Port);
        }
    }

    /// <summary>收到一段数据（一个 TCP 包内到达的字节块）。</summary>
    public class TcpDataEventArgs : EventArgs
    {
        public TcpDataEventArgs(long clientId, byte[] data)
        {
            ClientId = clientId;
            Data = data ?? new byte[0];
            Timestamp = DateTime.Now;
        }

        public TcpDataEventArgs(byte[] data)
            : this(0, data)
        {
        }

        /// <summary>服务端模式下为客户端编号；客户端模式下为 0。</summary>
        public long ClientId { get; private set; }

        /// <summary>数据本身（长度与到达的字节数一致）。</summary>
        public byte[] Data { get; private set; }

        public DateTime Timestamp { get; private set; }
    }

    /// <summary>客户端上线 / 下线事件。</summary>
    public class ClientEventArgs : EventArgs
    {
        public ClientEventArgs(ClientInfo client)
        {
            Client = client;
        }

        public ClientInfo Client { get; private set; }
    }

    /// <summary>监听成功 / 停止监听事件。</summary>
    public class TcpListeningEventArgs : EventArgs
    {
        public TcpListeningEventArgs(int port, string address)
        {
            Port = port;
            Address = address;
        }

        public int Port { get; private set; }
        public string Address { get; private set; }
    }

    /// <summary>客户端连接成功事件。</summary>
    public class TcpConnectedEventArgs : EventArgs
    {
        public TcpConnectedEventArgs(IPEndPoint local, IPEndPoint remote)
        {
            LocalAddress = local != null ? local.Address + ":" + local.Port : "-";
            RemoteAddress = remote != null ? remote.Address + ":" + remote.Port : "-";
        }

        public string LocalAddress { get; private set; }
        public string RemoteAddress { get; private set; }
    }

    /// <summary>错误事件（不中断程序的通信错误）。</summary>
    public class TcpErrorEventArgs : EventArgs
    {
        public TcpErrorEventArgs(string message, Exception exception)
        {
            Message = message;
            Exception = exception;
        }

        public string Message { get; private set; }
        public Exception Exception { get; private set; }
    }

    /// <summary>收发字节统计（线程安全）。</summary>
    public sealed class TrafficStats
    {
        private long _sentBytes;
        private long _receivedBytes;
        private long _sentPackets;
        private long _receivedPackets;

        public long SentBytes { get { return Interlocked.Read(ref _sentBytes); } }
        public long ReceivedBytes { get { return Interlocked.Read(ref _receivedBytes); } }
        public long SentPackets { get { return Interlocked.Read(ref _sentPackets); } }
        public long ReceivedPackets { get { return Interlocked.Read(ref _receivedPackets); } }

        public void AddSent(int count)
        {
            Interlocked.Add(ref _sentBytes, count);
            Interlocked.Increment(ref _sentPackets);
        }

        public void AddReceived(int count)
        {
            Interlocked.Add(ref _receivedBytes, count);
            Interlocked.Increment(ref _receivedPackets);
        }

        public void Reset()
        {
            Interlocked.Exchange(ref _sentBytes, 0);
            Interlocked.Exchange(ref _receivedBytes, 0);
            Interlocked.Exchange(ref _sentPackets, 0);
            Interlocked.Exchange(ref _receivedPackets, 0);
        }
    }
}
