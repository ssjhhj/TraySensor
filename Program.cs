using System;
using System.IO;
using System.Text;
using System.Windows.Forms;

namespace TraySensor
{
    internal static class Program
    {
        [STAThread]
        static void Main()
        {
            try
            {
                ApplicationConfiguration.Initialize();
                Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);

                Application.ThreadException += (sender, e) =>
                {
                    LogError("UI Thread Exception", e.Exception);
                    ShowErrorBox("运行时错误", e.Exception);
                };

                AppDomain.CurrentDomain.UnhandledException += (sender, e) =>
                {
                    var ex = e.ExceptionObject as Exception;
                    LogError("AppDomain Unhandled Exception", ex);
                };

                var ctx = new TrayApplicationContext();
                Application.Run(ctx);
            }
            catch (Exception ex)
            {
                LogError("Fatal Startup Exception", ex);
                ShowErrorBox("程序启动失败", ex);
            }
        }

        private static void ShowErrorBox(string title, Exception? ex)
        {
            try
            {
                string msg;
                if (ex != null)
                {
                    var sb = new StringBuilder();
                    sb.AppendLine($"【{title}】");
                    sb.AppendLine();
                    sb.AppendLine($"异常类型: {ex.GetType().FullName}");
                    sb.AppendLine($"错误消息: {ex.Message}");
                    sb.AppendLine();
                    sb.AppendLine("堆栈:");
                    sb.AppendLine(ex.StackTrace);

                    var inner = ex.InnerException;
                    int depth = 1;
                    while (inner != null && depth < 5)
                    {
                        sb.AppendLine();
                        sb.AppendLine($"-- 内部异常 #{depth} --");
                        sb.AppendLine($"类型: {inner.GetType().FullName}");
                        sb.AppendLine($"消息: {inner.Message}");
                        sb.AppendLine(inner.StackTrace);
                        inner = inner.InnerException;
                        depth++;
                    }
                    msg = sb.ToString();
                }
                else
                {
                    msg = $"【{title}】 未知错误";
                }

                MessageBox.Show(msg, "TraySensor 错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            catch
            {
            }
        }

        private static void LogError(string category, Exception? ex)
        {
            try
            {
                string logPath = Path.Combine(AppContext.BaseDirectory, "TraySensor_Error.log");
                var sb = new StringBuilder();
                sb.AppendLine($"========== {DateTime.Now:yyyy-MM-dd HH:mm:ss} :: {category} ==========");
                if (ex != null)
                {
                    sb.AppendLine($"Type: {ex.GetType().FullName}");
                    sb.AppendLine($"Message: {ex.Message}");
                    sb.AppendLine(ex.StackTrace);
                    var inner = ex.InnerException;
                    int depth = 1;
                    while (inner != null && depth < 5)
                    {
                        sb.AppendLine($"  Inner #{depth}: {inner.GetType().Name}: {inner.Message}");
                        sb.AppendLine($"  {inner.StackTrace}");
                        inner = inner.InnerException;
                        depth++;
                    }
                }
                sb.AppendLine();
                File.AppendAllText(logPath, sb.ToString(), Encoding.UTF8);
            }
            catch
            {
            }
        }
    }
}
