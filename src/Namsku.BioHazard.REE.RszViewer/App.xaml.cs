using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;

namespace RszViewer
{
    public partial class App : Application
    {
        public App()
        {
            DispatcherUnhandledException += App_DispatcherUnhandledException;
            AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;
            TaskScheduler.UnobservedTaskException += TaskScheduler_UnobservedTaskException;
        }

        private void App_DispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
        {
            LogException("DispatcherUnhandledException", e.Exception);
            e.Handled = true; // Prevent default crash dialog if possible, though we might still want to shut down
            Shutdown();
        }

        private void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            LogException("CurrentDomain_UnhandledException", e.ExceptionObject as Exception);
        }

        private void TaskScheduler_UnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs e)
        {
            LogException("TaskScheduler_UnobservedTaskException", e.Exception);
            e.SetObserved();
        }

        private void LogException(string source, Exception? ex)
        {
            try
            {
                var sb = new StringBuilder();
                sb.AppendLine($"--- Crash Detected at {DateTime.Now} ---");
                sb.AppendLine($"Source: {source}");
                if (ex != null)
                {
                    sb.AppendLine($"Message: {ex.Message}");
                    sb.AppendLine($"Stack Trace: {ex.StackTrace}");
                    if (ex.InnerException != null)
                    {
                        sb.AppendLine($"Inner Exception: {ex.InnerException.Message}");
                        sb.AppendLine($"Inner Stack Trace: {ex.InnerException.StackTrace}");
                    }
                }
                else
                {
                    sb.AppendLine("Exception object was null.");
                }
                sb.AppendLine("------------------------------------------");
                sb.AppendLine();

                File.AppendAllText("crash.log", sb.ToString());
                MessageBox.Show($"Application crashed! See crash.log for details.\n\nError: {ex?.Message}", "Crash Report", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            catch
            {
                // Last resort if logging fails
                MessageBox.Show($"Application crashed and logging failed.\n\nError: {ex?.Message}", "Fatal Crash", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}
