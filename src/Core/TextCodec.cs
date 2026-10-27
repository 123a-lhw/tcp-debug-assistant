using System;
using System.Text;

namespace TcpTool.Core
{
    /// <summary>文本编码的编解码，以及把不可见字符转成可读的显示形式。</summary>
    public static class TextCodec
    {
        public const string Utf8 = "UTF-8";
        public const string Gbk = "GBK";
        public const string Ascii = "ASCII";
        public const string Latin1 = "Latin1";

        /// <summary>界面上可选的编码名，第一个为默认值。</summary>
        public static readonly string[] Names = new string[] { Utf8, Gbk, Ascii, Latin1 };

        /// <summary>把界面上的编码名解析成 Encoding 实例（未知名字退回 UTF-8）。</summary>
        public static Encoding GetEncoding(string name)
        {
            string n = (name ?? string.Empty).Trim();
            string upper = n.ToUpperInvariant();

            if (upper.Length == 0 || upper == "UTF-8" || upper == "UTF8")
                return new UTF8Encoding(false, false);

            if (upper == "GBK" || upper == "GB2312" || upper == "GB18030")
                return Encoding.GetEncoding("GB18030");

            if (upper == "ASCII" || upper == "US-ASCII")
                return Encoding.GetEncoding("us-ascii", EncoderFallback.ReplacementFallback, DecoderFallback.ReplacementFallback);

            if (upper == "LATIN1" || upper == "ISO-8859-1")
                return Encoding.GetEncoding("iso-8859-1");

            try
            {
                return Encoding.GetEncoding(n);
            }
            catch (ArgumentException)
            {
                return new UTF8Encoding(false, false);
            }
            catch (NotSupportedException)
            {
                return new UTF8Encoding(false, false);
            }
        }

        /// <summary>编码为字节。</summary>
        public static byte[] Encode(string text, string encodingName)
        {
            return GetEncoding(encodingName).GetBytes(text ?? string.Empty);
        }

        /// <summary>把字节解码为文本。</summary>
        public static string Decode(byte[] data, int offset, int count, string encodingName)
        {
            if (data == null || count <= 0) return string.Empty;
            return GetEncoding(encodingName).GetString(data, offset, count);
        }

        /// <summary>
        /// 把控制字符转成可读形式，便于在接收区显示：
        /// 换行统一为 CRLF，制表符显示为 \t，其它控制字符显示为 \xNN。
        /// </summary>
        public static string EscapeControlChars(string text)
        {
            if (string.IsNullOrEmpty(text)) return string.Empty;

            StringBuilder sb = new StringBuilder(text.Length + 8);
            for (int i = 0; i < text.Length; i++)
            {
                char c = text[i];

                if (c == '\r')
                {
                    // 与后面的 \n 组成一个换行，避免出现两个空行
                    if (i + 1 < text.Length && text[i + 1] == '\n') continue;
                    sb.Append("\r\n");
                    continue;
                }
                if (c == '\n')
                {
                    sb.Append("\r\n");
                    continue;
                }
                if (c == '\t')
                {
                    sb.Append("\\t");
                    continue;
                }
                if (c < ' ' || c == (char)0x7F)
                {
                    sb.Append("\\x");
                    sb.Append(((int)c).ToString("X2"));
                    continue;
                }
                sb.Append(c);
            }
            return sb.ToString();
        }

        /// <summary>字节数的可读形式，例如 "1.5 KB"。</summary>
        public static string FormatBytes(long bytes)
        {
            if (bytes < 1024L) return bytes.ToString() + " B";
            double kb = bytes / 1024.0;
            if (kb < 1024.0) return kb.ToString("0.##") + " KB";
            double mb = kb / 1024.0;
            if (mb < 1024.0) return mb.ToString("0.##") + " MB";
            return (mb / 1024.0).ToString("0.##") + " GB";
        }
    }

    /// <summary>
    /// 支持跨数据包边界的增量解码器：多字节字符被拆成两个 TCP 包时不会乱码。
    /// 每条连接（或每个方向）应各自持有一个实例。
    /// </summary>
    public sealed class StreamTextDecoder
    {
        private Encoding _encoding;
        private Decoder _decoder;

        public StreamTextDecoder(string encodingName)
        {
            SetEncoding(encodingName);
        }

        public string EncodingName
        {
            get { return _encoding.WebName; }
        }

        public void SetEncoding(string encodingName)
        {
            _encoding = TextCodec.GetEncoding(encodingName);
            _decoder = _encoding.GetDecoder();
        }

        /// <summary>解码一段数据，未完成的字符会保留到下一次调用。</summary>
        public string Decode(byte[] data, int offset, int count)
        {
            if (data == null || count <= 0) return string.Empty;
            char[] chars = new char[_encoding.GetMaxCharCount(count)];
            int n = _decoder.GetChars(data, offset, count, chars, 0, false);
            return new string(chars, 0, n);
        }

        /// <summary>冲刷解码器内部残留的半个字符（切换连接/停止时调用）。</summary>
        public string Flush()
        {
            char[] chars = new char[16];
            int n = _decoder.GetChars(new byte[0], 0, 0, chars, 0, true);
            return new string(chars, 0, n);
        }
    }
}
