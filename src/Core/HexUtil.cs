using System;
using System.Collections.Generic;
using System.Text;

namespace TcpTool.Core
{
    /// <summary>
    /// 十六进制文本与字节数组之间的转换。
    /// 支持 "01 02 0A"、"01020A"、"0x01,0x02"、"01-02" 等写法。
    /// </summary>
    public static class HexUtil
    {
        private static readonly char[] Separators =
            new char[] { ' ', '\t', '\r', '\n', ',', ';', ':', '-', '|' };

        /// <summary>尝试解析十六进制文本；失败时 error 给出可读的中文原因。</summary>
        public static bool TryParse(string text, out byte[] data)
        {
            string error;
            return TryParse(text, out data, out error);
        }

        /// <summary>尝试解析十六进制文本；失败时 error 给出可读的中文原因。</summary>
        public static bool TryParse(string text, out byte[] data, out string error)
        {
            data = new byte[0];
            error = null;
            if (string.IsNullOrEmpty(text)) return true;

            List<byte> bytes = new List<byte>(text.Length / 2 + 1);
            string[] parts = text.Split(Separators, StringSplitOptions.RemoveEmptyEntries);

            for (int i = 0; i < parts.Length; i++)
            {
                string original = parts[i];
                string part = original;

                // 兼容 0x / 0X 前缀，例如 "0x0A"
                if (part.Length > 2 && part.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
                    part = part.Substring(2);
                if (part.Length == 0) continue;

                if ((part.Length & 1) == 1)
                {
                    error = string.Format("第 {0} 个片段“{1}”是奇数个字符，无法凑成整字节。", i + 1, original);
                    return false;
                }

                for (int k = 0; k < part.Length; k += 2)
                {
                    int hi = HexValue(part[k]);
                    int lo = HexValue(part[k + 1]);
                    if (hi < 0 || lo < 0)
                    {
                        char bad = hi < 0 ? part[k] : part[k + 1];
                        error = string.Format("第 {0} 个片段“{1}”含有非十六进制字符“{2}”。", i + 1, original, bad);
                        return false;
                    }
                    bytes.Add((byte)((hi << 4) | lo));
                }
            }

            data = bytes.ToArray();
            return true;
        }

        /// <summary>把字节数组格式化为 "01 0A FF" 形式（大写，空格分隔）。</summary>
        public static string Format(byte[] data)
        {
            if (data == null) return string.Empty;
            return Format(data, 0, data.Length, " ");
        }

        /// <summary>按指定分隔符格式化字节数组。</summary>
        public static string Format(byte[] data, int offset, int count, string separator)
        {
            if (data == null || count <= 0) return string.Empty;
            if (offset < 0 || offset + count > data.Length)
                throw new ArgumentOutOfRangeException("count", "越界访问字节数组。");

            bool useSep = !string.IsNullOrEmpty(separator);
            int sepLen = useSep ? separator.Length : 0;
            StringBuilder sb = new StringBuilder(count * (2 + sepLen));

            for (int i = 0; i < count; i++)
            {
                if (i > 0 && useSep) sb.Append(separator);
                int value = data[offset + i];
                sb.Append(HexChars[value >> 4]);
                sb.Append(HexChars[value & 0x0F]);
            }
            return sb.ToString();
        }

        /// <summary>把任意写法规范化为标准 "01 0A" 形式。</summary>
        public static bool TryNormalize(string text, out string normalized, out string error)
        {
            byte[] data;
            if (!TryParse(text, out data, out error))
            {
                normalized = null;
                return false;
            }
            normalized = Format(data);
            return true;
        }

        private static readonly char[] HexChars = new char[]
        {
            '0', '1', '2', '3', '4', '5', '6', '7', '8', '9', 'A', 'B', 'C', 'D', 'E', 'F'
        };

        private static int HexValue(char c)
        {
            if (c >= '0' && c <= '9') return c - '0';
            if (c >= 'a' && c <= 'f') return c - 'a' + 10;
            if (c >= 'A' && c <= 'F') return c - 'A' + 10;
            return -1;
        }
    }
}
