using System;
using System.Windows.Forms;

namespace TcpTool.App
{
    internal static class Program
    {
        [STAThread]
        private static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            Application.ThreadException += delegate(object sender, System.Threading.ThreadExceptionEventArgs e)
            {
                ShowFatal(e.Exception);
            };
            AppDomain.CurrentDomain.UnhandledException += delegate(object sender, UnhandledExceptionEventArgs e)
            {
                ShowFatal(e.ExceptionObject as Exception);
            };

            Application.Run(new MainForm());
        }

        private static void ShowFatal(Exception ex)
        {
            if (ex == null) return;
            try
            {
                MessageBox.Show(ex.ToString(), "程序错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            catch (Exception)
            {
                // 连错误框都弹不出来时只能放弃
            }
        }
    }
}
