using System;
using System.Windows;
using System.Windows.Threading;
using System.Runtime.Versioning;

namespace WinDock
{
    /// <summary>
    /// App.xaml 的交互逻辑
    /// </summary>
    [SupportedOSPlatform("windows")]
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            DispatcherUnhandledException += App_DispatcherUnhandledException;
            try
            {
                base.OnStartup(e);
                var window = new MainWindow();
                MainWindow = window;
                window.Show();
            }
            catch (Exception ex)
            {
                LogStartupError(ex);
                ShowStartupError(ex);
                Shutdown(-1);
            }
        }

        private void App_DispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
        {
            LogStartupError(e.Exception);
            ShowStartupError(e.Exception);
            e.Handled = true;
            Shutdown(-1);
        }

        private static void ShowStartupError(Exception exception)
        {
            MessageBox.Show(
                "WinDock 启动失败：\n\n" + exception,
                "WinDock 启动错误",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }

        private static void LogStartupError(Exception exception)
        {
            try
            {
                var path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "WinDock-startup-error.txt");
                System.IO.File.WriteAllText(path, DateTime.Now + Environment.NewLine + exception);
            }
            catch (Exception)
            {
                // 启动错误记录失败时不再抛出二次异常。
            }
        }
    }
}
