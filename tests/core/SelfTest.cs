using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using TcpTool.Core;

namespace TcpTool.Tests
{
    /// <summary>
    /// 核心库自检：十六进制/编码转换、TCP 服务端与客户端会话的真实回环收发。
    /// 全部通过时退出码为 0。
    /// </summary>
    internal static class SelfTest
    {
        private static int _passed;
        private static readonly List<string> Failures = new List<string>();

        private static int Main()
        {
            Console.OutputEncoding = Encoding.UTF8;
            Console.WriteLine("TCP 调试助手 · 核心自检");
            Console.WriteLine("------------------------------------------");

            CheckHex();
            CheckCodec();
            CheckStats();
            CheckServerLoopback();
            CheckServerBroadcast();
            CheckClientSession();
            CheckServerStop();
            CheckConnectRefused();

            Console.WriteLine("------------------------------------------");
            Console.WriteLine(string.Format("通过 {0} 项，失败 {1} 项", _passed, Failures.Count));
            if (Failures.Count > 0)
            {
                foreach (string failure in Failures)
                {
                    Console.WriteLine("  失败：" + failure);
                }
                return 1;
            }
            Console.WriteLine("全部自检通过。");
            return 0;
        }

        // ---------------- 十六进制 ----------------

        private static void CheckHex()
        {
            Check("十六进制：空格/逗号分隔", delegate
            {
                byte[] data;
                Require(HexUtil.TryParse("01 0A,ff", out data), "解析应成功");
                Require(data.Length == 3, "应为 3 字节，实际 " + data.Length);
                Require(data[0] == 1 && data[1] == 10 && data[2] == 255, "字节内容不正确");
                return true;
            });

            Check("十六进制：无分隔符连写", delegate
            {
                byte[] data;
                Require(HexUtil.TryParse("0102AB", out data), "解析应成功");
                Require(data.Length == 3 && data[2] == 0xAB, "应为 01 02 AB");
                return true;
            });

            Check("十六进制：0x 前缀", delegate
            {
                byte[] data;
                Require(HexUtil.TryParse("0x01, 0x02", out data), "解析应成功");
                Require(data.Length == 2 && data[0] == 1 && data[1] == 2, "应为 01 02");
                return true;
            });

            Check("十六进制：奇数长度应报错", delegate
            {
                byte[] data;
                string error;
                Require(!HexUtil.TryParse("0A0", out data, out error), "应当解析失败");
                Require(!string.IsNullOrEmpty(error), "应给出中文错误说明");
                return true;
            });

            Check("十六进制：非法字符应报错", delegate
            {
                byte[] data;
                string error;
                Require(!HexUtil.TryParse("0G", out data, out error), "应当解析失败");
                Require(!string.IsNullOrEmpty(error), "应给出中文错误说明");
                return true;
            });

            Check("十六进制：格式化与往返一致", delegate
            {
                byte[] original = new byte[64];
                new Random(7).NextBytes(original);
                string text = HexUtil.Format(original);
                Require(text.StartsWith("00") || text.Length == 64 * 3 - 1, "格式长度异常：" + text.Length);

                byte[] parsed;
                Require(HexUtil.TryParse(text, out parsed), "格式化结果应可解析");
                Require(parsed.Length == original.Length, "长度应一致");
                for (int i = 0; i < original.Length; i++)
                {
                    Require(parsed[i] == original[i], "第 " + i + " 字节不一致");
                }
                return true;
            });

            Check("十六进制：空内容解析为空数组", delegate
            {
                byte[] data;
                Require(HexUtil.TryParse("   ", out data), "解析应成功");
                Require(data.Length == 0, "应为空数组");
                return true;
            });
        }

        // ---------------- 编码 ----------------

        private static void CheckCodec()
        {
            Check("UTF-8 编解码往返", delegate
            {
                string text = "你好, TCP 调试助手";
                byte[] bytes = TextCodec.Encode(text, TextCodec.Utf8);
                string back = TextCodec.Decode(bytes, 0, bytes.Length, TextCodec.Utf8);
                Require(back == text, "往返结果不一致：" + back);
                return true;
            });

            Check("GBK 编解码往返（4 个汉字 = 8 字节）", delegate
            {
                byte[] bytes = TextCodec.Encode("中文测试", TextCodec.Gbk);
                Require(bytes.Length == 8, "GBK 长度应为 8，实际 " + bytes.Length);
                string back = TextCodec.Decode(bytes, 0, bytes.Length, TextCodec.Gbk);
                Require(back == "中文测试", "往返结果不一致：" + back);
                return true;
            });

            Check("控制字符转义显示", delegate
            {
                string shown = TextCodec.EscapeControlChars("A\u0001B\nC");
                Require(shown.Contains("\\x01"), "应包含 \\x01：" + shown);
                Require(shown.Contains("A\\x01B\r\nC"), "换行应转成 CRLF：" + shown);
                return true;
            });

            Check("增量解码：多字节字符被拆包不乱码", delegate
            {
                byte[] bytes = Encoding.UTF8.GetBytes("中");
                Require(bytes.Length == 3, "UTF-8 汉字应为 3 字节");

                StreamTextDecoder decoder = new StreamTextDecoder(TextCodec.Utf8);
                string first = decoder.Decode(bytes, 0, 1);
                string second = decoder.Decode(bytes, 1, 2);
                Require(first.Length == 0, "第一个字节应无法独立成字，实际：" + first);
                Require(first + second == "中", "拼接结果应为“中”，实际：" + first + second);
                return true;
            });

            Check("字节数格式化", delegate
            {
                Require(TextCodec.FormatBytes(512) == "512 B", "512 B 格式化错误");
                Require(TextCodec.FormatBytes(1536) == "1.5 KB", "1.5 KB 格式化错误：" + TextCodec.FormatBytes(1536));
                return true;
            });
        }

        // ---------------- 统计 ----------------

        private static void CheckStats()
        {
            Check("收发计数线程安全累加", delegate
            {
                TrafficStats stats = new TrafficStats();
                List<Thread> threads = new List<Thread>();
                for (int t = 0; t < 4; t++)
                {
                    Thread thread = new Thread(delegate()
                    {
                        for (int i = 0; i < 1000; i++) stats.AddSent(1);
                    });
                    thread.Start();
                    threads.Add(thread);
                }
                foreach (Thread thread in threads) thread.Join();

                Require(stats.SentBytes == 4000, "应为 4000，实际 " + stats.SentBytes);
                Require(stats.SentPackets == 4000, "包数应为 4000");
                stats.Reset();
                Require(stats.SentBytes == 0, "重置后应为 0");
                return true;
            });
        }

        // ---------------- 服务端 ----------------

        private static void CheckServerLoopback()
        {
            Check("服务端：接受连接、收数据、定向回发", delegate
            {
                using (TcpServer server = new TcpServer())
                {
                    ManualResetEventSlim connectedSignal = new ManualResetEventSlim(false);
                    ManualResetEventSlim dataSignal = new ManualResetEventSlim(false);
                    byte[] receivedPayload = null;
                    long clientId = 0;

                    server.ClientConnected += delegate(object sender, ClientEventArgs e)
                    {
                        clientId = e.Client.Id;
                        connectedSignal.Set();
                    };
                    server.DataReceived += delegate(object sender, TcpDataEventArgs e)
                    {
                        receivedPayload = e.Data;
                        dataSignal.Set();
                    };

                    int port = GetFreePort();
                    server.Start(port);
                    Require(server.IsRunning, "服务端应处于监听状态");
                    Require(server.Port == port, "监听端口应为 " + port);

                    TcpClient raw = new TcpClient();
                    try
                    {
                        raw.Connect(IPAddress.Loopback, port);
                        WaitOrThrow(connectedSignal, 3000, "服务端触发 ClientConnected");
                        Require(server.ClientCount == 1, "客户端数应为 1，实际 " + server.ClientCount);

                        byte[] hello = Encoding.UTF8.GetBytes("ping-中文");
                        NetworkStream stream = raw.GetStream();
                        stream.Write(hello, 0, hello.Length);
                        stream.Flush();

                        WaitOrThrow(dataSignal, 3000, "服务端收到数据");
                        Require(receivedPayload.Length == hello.Length, "收到字节数不符");
                        Require(Encoding.UTF8.GetString(receivedPayload) == "ping-中文", "收到的内容不符");

                        int sentCount = server.SendTo(clientId, Encoding.UTF8.GetBytes("pong"));
                        Require(sentCount == 1, "定向发送应成功 1 个客户端");

                        byte[] buffer = new byte[16];
                        stream.ReadTimeout = 3000;
                        int read = stream.Read(buffer, 0, buffer.Length);
                        Require(Encoding.UTF8.GetString(buffer, 0, read) == "pong", "客户端未收到 pong");
                    }
                    finally
                    {
                        raw.Close();
                    }
                }
                return true;
            });
        }

        private static void CheckServerBroadcast()
        {
            Check("服务端：多客户端广播", delegate
            {
                using (TcpServer server = new TcpServer())
                {
                    int port = GetFreePort();
                    server.Start(port);

                    TcpClient a = new TcpClient();
                    TcpClient b = new TcpClient();
                    try
                    {
                        a.Connect(IPAddress.Loopback, port);
                        b.Connect(IPAddress.Loopback, port);
                        WaitUntil(delegate { return server.ClientCount == 2; }, 3000, "两个客户端应都已接入");

                        byte[] payload = Encoding.UTF8.GetBytes("broadcast!");
                        int sent = server.Broadcast(payload);
                        Require(sent == 2, "广播应命中 2 个客户端，实际 " + sent);

                        foreach (TcpClient client in new TcpClient[] { a, b })
                        {
                            NetworkStream stream = client.GetStream();
                            stream.ReadTimeout = 3000;
                            byte[] buffer = new byte[32];
                            int read = stream.Read(buffer, 0, buffer.Length);
                            Require(Encoding.UTF8.GetString(buffer, 0, read) == "broadcast!", "广播内容不符");
                        }
                    }
                    finally
                    {
                        a.Close();
                        b.Close();
                    }
                }
                return true;
            });
        }

        private static void CheckServerStop()
        {
            Check("服务端：停止监听后清空连接", delegate
            {
                TcpServer server = new TcpServer();
                int port = GetFreePort();
                ManualResetEventSlim stoppedSignal = new ManualResetEventSlim(false);
                server.Stopped += delegate { stoppedSignal.Set(); };

                server.Start(port);
                TcpClient raw = new TcpClient();
                try
                {
                    raw.Connect(IPAddress.Loopback, port);
                    WaitUntil(delegate { return server.ClientCount == 1; }, 3000, "客户端应已接入");

                    server.Stop();
                    Require(!server.IsRunning, "停止后应不在监听状态");
                    Require(server.ClientCount == 0, "停止后客户端列表应为空");
                    WaitOrThrow(stoppedSignal, 2000, "Stopped 事件");
                }
                finally
                {
                    raw.Close();
                    server.Dispose();
                }
                return true;
            });
        }

        // ---------------- 客户端会话 ----------------

        private static void CheckClientSession()
        {
            Check("客户端会话：连接、发送、接收、断开事件", delegate
            {
                using (TcpServer server = new TcpServer())
                using (TcpClientSession session = new TcpClientSession())
                {
                    ManualResetEventSlim serverGot = new ManualResetEventSlim(false);
                    byte[] serverData = null;
                    server.DataReceived += delegate(object sender, TcpDataEventArgs e)
                    {
                        serverData = e.Data;
                        serverGot.Set();
                    };

                    int port = GetFreePort();
                    server.Start(port);

                    ManualResetEventSlim connectedSignal = new ManualResetEventSlim(false);
                    session.Connected += delegate { connectedSignal.Set(); };
                    session.Connect("127.0.0.1", port, 3000);
                    WaitOrThrow(connectedSignal, 3000, "Connected 事件");
                    Require(session.IsConnected, "应处于已连接状态");
                    Require(session.RemoteEndPoint.EndsWith(":" + port), "远端端点异常：" + session.RemoteEndPoint);

                    session.Send(Encoding.UTF8.GetBytes("from-client"));
                    WaitOrThrow(serverGot, 3000, "服务端收到客户端数据");
                    Require(Encoding.UTF8.GetString(serverData) == "from-client", "服务端收到的内容不符");

                    ManualResetEventSlim clientGot = new ManualResetEventSlim(false);
                    byte[] clientData = null;
                    session.DataReceived += delegate(object sender, TcpDataEventArgs e)
                    {
                        clientData = e.Data;
                        clientGot.Set();
                    };

                    server.Broadcast(Encoding.UTF8.GetBytes("to-client"));
                    WaitOrThrow(clientGot, 3000, "客户端收到服务端数据");
                    Require(Encoding.UTF8.GetString(clientData) == "to-client", "客户端收到的内容不符");

                    ManualResetEventSlim closedSignal = new ManualResetEventSlim(false);
                    session.Closed += delegate { closedSignal.Set(); };
                    session.Close();
                    WaitOrThrow(closedSignal, 2000, "Closed 事件");
                    Require(!session.IsConnected, "断开后不应处于已连接状态");
                    Require(!session.Send(new byte[] { 1 }), "断开后发送应返回 false");
                }
                return true;
            });
        }

        private static void CheckConnectRefused()
        {
            Check("客户端会话：连接未监听端口应报错", delegate
            {
                using (TcpClientSession session = new TcpClientSession())
                {
                    int port = GetFreePort();
                    bool threw = false;
                    try
                    {
                        session.Connect("127.0.0.1", port, 1500);
                    }
                    catch (Exception)
                    {
                        threw = true;
                    }
                    Require(threw, "连接未监听端口应抛出异常");
                    Require(!session.IsConnected, "失败后不应处于已连接状态");
                }
                return true;
            });
        }

        // ---------------- 断言与等待工具 ----------------

        private static void Check(string name, Func<bool> body)
        {
            try
            {
                if (body())
                {
                    _passed++;
                    Console.WriteLine("[通过] " + name);
                }
                else
                {
                    Failures.Add(name);
                    Console.WriteLine("[失败] " + name);
                }
            }
            catch (Exception ex)
            {
                Failures.Add(name + "：" + ex.Message);
                Console.WriteLine("[失败] " + name + " —— " + ex.Message);
            }
        }

        private static void Require(bool condition, string message)
        {
            if (!condition) throw new Exception(message);
        }

        private static void WaitOrThrow(ManualResetEventSlim signal, int timeoutMs, string what)
        {
            if (!signal.Wait(timeoutMs)) throw new Exception("等待超时：" + what);
        }

        private static void WaitUntil(Func<bool> condition, int timeoutMs, string what)
        {
            DateTime deadline = DateTime.Now.AddMilliseconds(timeoutMs);
            while (DateTime.Now < deadline)
            {
                if (condition()) return;
                Thread.Sleep(10);
            }
            throw new Exception("等待超时：" + what);
        }

        private static int GetFreePort()
        {
            TcpListener probe = new TcpListener(IPAddress.Loopback, 0);
            probe.Start();
            int port = ((IPEndPoint)probe.LocalEndpoint).Port;
            probe.Stop();
            return port;
        }
    }
}
