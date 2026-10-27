using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace TcpTool.App
{
    /// <summary>把界面上的常用设置保存到 %APPDATA%\TcpDebugAssistant\settings.ini。</summary>
    internal static class SettingsStore
    {
        private static string DirectoryPath
        {
            get
            {
                return Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                    "TcpDebugAssistant");
            }
        }

        private static string FilePath
        {
            get { return Path.Combine(DirectoryPath, "settings.ini"); }
        }

        public static Dictionary<string, string> Load()
        {
            Dictionary<string, string> values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            try
            {
                if (!File.Exists(FilePath)) return values;

                foreach (string line in File.ReadAllLines(FilePath, Encoding.UTF8))
                {
                    if (line.Length == 0) continue;
                    int index = line.IndexOf('=');
                    if (index <= 0) continue;
                    string key = line.Substring(0, index).Trim();
                    string value = line.Substring(index + 1).Trim();
                    if (key.Length == 0) continue;
                    values[key] = value;
                }
            }
            catch (IOException)
            {
                // 读不到就用默认值，不影响使用
            }
            catch (UnauthorizedAccessException)
            {
            }
            return values;
        }

        public static void Save(Dictionary<string, string> values)
        {
            if (values == null) return;
            try
            {
                Directory.CreateDirectory(DirectoryPath);
                StringBuilder sb = new StringBuilder();
                foreach (KeyValuePair<string, string> pair in values)
                {
                    sb.Append(pair.Key).Append('=').Append(pair.Value).Append("\r\n");
                }
                File.WriteAllText(FilePath, sb.ToString(), new UTF8Encoding(false));
            }
            catch (IOException)
            {
            }
            catch (UnauthorizedAccessException)
            {
            }
        }
    }
}
