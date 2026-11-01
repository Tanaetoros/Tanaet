using System;
using System.Drawing;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace HomeworkDock
{
    public class HomeworkApplication : Application
    {
        public static Action ActivateRequested;
        public static StateStore Store;
        public static AppState State;
        public static bool StartupLaunch;
        public static bool NoAutostart;

        /// <summary>
        /// Entry point used by run.ps1. WPF must live on an STA thread even
        /// when the caller (Windows PowerShell) is already STA.
        /// </summary>
        public static void Run(string[] args)
        {
            Thread thread = new Thread(delegate()
            {
                try
                {
                    Main(args);
                }
                catch (Exception ex)
                {
                    WriteCrashLog("Host run failed", ex);
                }
            });
            thread.SetApartmentState(ApartmentState.STA);
            thread.IsBackground = false;
            thread.Name = "HomeworkDock.UI";
            thread.Start();
            thread.Join();
        }

        [STAThread]
        public static void Main(string[] args)
        {
            AppDomain.CurrentDomain.UnhandledException += delegate(object sender, UnhandledExceptionEventArgs e)
            {
                WriteCrashLog("UnhandledException", e.ExceptionObject);
            };

            bool createdNew;
            Mutex mutex = new Mutex(true, "Local\\HomeworkDock.SingleInstance", out createdNew);
            if (!createdNew)
            {
                try
                {
                    EventWaitHandle existing = EventWaitHandle.OpenExisting("Local\\HomeworkDock.Activate");
                    existing.Set();
                    existing.Close();
                }
                catch
                {
                }
                return;
            }

            EventWaitHandle activateEvent;
            try
            {
                activateEvent = new EventWaitHandle(false, EventResetMode.AutoReset, "Local\\HomeworkDock.Activate");
            }
            catch
            {
                activateEvent = null;
            }

            HomeworkApplication app = new HomeworkApplication();
            app.ShutdownMode = ShutdownMode.OnExplicitShutdown;
            app.Startup += delegate { app.StartApplication(args); };

            if (activateEvent != null)
            {
                Thread listener = new Thread(delegate()
                {
                    while (true)
                    {
                        activateEvent.WaitOne();
                        try
                        {
                            app.Dispatcher.BeginInvoke(new Action(delegate
                            {
                                if (ActivateRequested != null) ActivateRequested();
                            }));
                        }
                        catch
                        {
                        }
                    }
                });
                listener.IsBackground = true;
                listener.Name = "HomeworkDock.ActivateListener";
                listener.Start();
            }

            try
            {
                app.Run();
            }
            finally
            {
                try { mutex.ReleaseMutex(); }
                catch { }
                try { if (activateEvent != null) activateEvent.Close(); }
                catch { }
            }
        }

        private MainWindow _window;

        private void StartApplication(string[] args)
        {
            DispatcherUnhandledException += delegate(object sender, DispatcherUnhandledExceptionEventArgs e)
            {
                SafeLog("DispatcherUnhandledException: " + e.Exception);
                WriteCrashLog("DispatcherUnhandledException", e.Exception);
                MessageBox.Show("作业清单遇到一个错误，已记录日志。\n\n" + e.Exception.Message,
                    "作业清单", MessageBoxButton.OK, MessageBoxImage.Warning);
                e.Handled = true;
            };

            try
            {
                NoAutostart = HasArgument(args, "--no-autostart");
                StartupLaunch = HasArgument(args, "--startup");

                Store = new StateStore();
                State = Store.Load();

                if (NoAutostart) State.Settings.Autostart = false;

                _window = new MainWindow(Store, State, StartupLaunch, NoAutostart);
                _window.ShowActivated = !StartupLaunch;
                ActivateRequested = delegate { _window.ShowFromTray(); };
                _window.Show();

                if (!string.IsNullOrEmpty(Store.InitWarning))
                {
                    WriteCrashLog("Data directory warning", Store.InitWarning);
                    MessageBox.Show(Store.InitWarning + "\n\n当前数据位置：" + Store.DataDir,
                        "作业清单", MessageBoxButton.OK, MessageBoxImage.Warning);
                }

                if (Store.WasFirstRun)
                {
                    try
                    {
                        Store.Save(State);
                    }
                    catch (Exception ex)
                    {
                        Store.Log("Initial save failed: " + ex);
                        WriteCrashLog("InitialSave failed", ex);
                    }
                }

                SessionEnding += delegate
                {
                    _window.PrepareForShutdown();
                };
            }
            catch (Exception ex)
            {
                WriteCrashLog("StartApplication failed", ex);
                MessageBox.Show("作业清单启动失败。\n\n" + ex.Message +
                    "\n\n详细日志：" + Path.Combine(Path.GetTempPath(), "HomeworkDock-crash.log"),
                    "作业清单", MessageBoxButton.OK, MessageBoxImage.Error);
                Shutdown(1);
            }
        }

        private static void WriteCrashLog(string context, object error)
        {
            try
            {
                string path = Path.Combine(Path.GetTempPath(), "HomeworkDock-crash.log");
                File.AppendAllText(path,
                    DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss", System.Globalization.CultureInfo.InvariantCulture) +
                    "  " + context + Environment.NewLine +
                    (error == null ? "(null)" : error.ToString()) + Environment.NewLine + Environment.NewLine,
                    new System.Text.UTF8Encoding(false));
            }
            catch
            {
            }
        }

        private static bool HasArgument(string[] args, string value)
        {
            if (args == null) return false;
            for (int i = 0; i < args.Length; i++)
            {
                if (string.Equals(args[i], value, StringComparison.OrdinalIgnoreCase)) return true;
            }
            return false;
        }

        private static void SafeLog(string message)
        {
            try
            {
                if (Store != null) Store.Log(message);
            }
            catch
            {
            }
        }
    }

    public static class AppIcon
    {
        public static System.Windows.Media.ImageSource Load()
        {
            try
            {
                using (Icon icon = AppHost.LoadIcon())
                {
                    if (icon == null) return null;
                    BitmapSource source = Imaging.CreateBitmapSourceFromHIcon(
                        icon.Handle, Int32Rect.Empty, BitmapSizeOptions.FromEmptyOptions());
                    source.Freeze();
                    return source;
                }
            }
            catch
            {
                return null;
            }
        }
    }
}
