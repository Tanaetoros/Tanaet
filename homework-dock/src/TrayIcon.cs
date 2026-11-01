using System;
using System.Drawing;
using System.IO;
using System.Reflection;
using System.Windows.Forms;

namespace HomeworkDock
{
    public class TrayIcon : IDisposable
    {
        private NotifyIcon _notifyIcon;
        private ContextMenuStrip _menu;
        private ToolStripMenuItem _autostartItem;
        private Icon _icon;
        private readonly Action _showAction;
        private readonly Action _hideAction;
        private readonly Action _exitAction;
        private readonly Func<bool> _getAutostart;
        private readonly Action<bool> _setAutostart;
        private readonly string _dataDir;
        private bool _disposed;

        public TrayIcon(Action showAction, Action hideAction, Action exitAction,
            Func<bool> getAutostart, Action<bool> setAutostart, string dataDir)
        {
            _showAction = showAction;
            _hideAction = hideAction;
            _exitAction = exitAction;
            _getAutostart = getAutostart;
            _setAutostart = setAutostart;
            _dataDir = dataDir;

            _icon = LoadAppIcon();
            _menu = BuildMenu();

            _notifyIcon = new NotifyIcon();
            _notifyIcon.Icon = _icon;
            _notifyIcon.Text = "作业清单";
            _notifyIcon.ContextMenuStrip = _menu;
            _notifyIcon.Visible = true;
            _notifyIcon.DoubleClick += delegate { SafeInvoke(_showAction); };
            _notifyIcon.MouseClick += delegate(object sender, MouseEventArgs e)
            {
                if (e.Button == MouseButtons.Left) SafeInvoke(_showAction);
            };
        }

        public void UpdateStatus(int pending, int done)
        {
            if (_notifyIcon == null) return;
            string text;
            if (pending == 0 && done == 0)
            {
                text = "作业清单 · 暂无待办";
            }
            else if (pending == 0)
            {
                text = "作业清单 · 已做完，记得交付";
            }
            else
            {
                text = "作业清单 · 还剩 " + pending + " 项";
            }
            try { _notifyIcon.Text = text; }
            catch { }
        }

        public void ShowBalloon(string title, string text)
        {
            if (_notifyIcon == null) return;
            try
            {
                _notifyIcon.BalloonTipTitle = title;
                _notifyIcon.BalloonTipText = text;
                _notifyIcon.ShowBalloonTip(2500);
            }
            catch { }
        }

        private ContextMenuStrip BuildMenu()
        {
            ContextMenuStrip menu = new ContextMenuStrip();
            menu.ShowImageMargin = false;
            menu.Font = new Font("Microsoft YaHei UI", 9.5f);
            menu.RenderMode = ToolStripRenderMode.System;

            ToolStripMenuItem show = new ToolStripMenuItem("显示作业清单");
            show.Font = new Font(menu.Font, System.Drawing.FontStyle.Bold);
            show.Click += delegate { SafeInvoke(_showAction); };
            menu.Items.Add(show);

            ToolStripMenuItem hide = new ToolStripMenuItem("隐藏窗口");
            hide.Click += delegate { SafeInvoke(_hideAction); };
            menu.Items.Add(hide);
            menu.Items.Add(new ToolStripSeparator());

            _autostartItem = new ToolStripMenuItem("开机自动启动");
            _autostartItem.Click += delegate
            {
                bool next = !_getAutostart();
                try { _setAutostart(next); }
                catch (Exception ex) { MessageBox.Show("设置开机启动失败：" + ex.Message, "作业清单"); }
            };
            menu.Items.Add(_autostartItem);

            ToolStripMenuItem openData = new ToolStripMenuItem("打开数据文件夹");
            openData.Click += delegate
            {
                try
                {
                    string dir = _dataDir;
                    if (string.IsNullOrEmpty(dir))
                    {
                        dir = Path.Combine(
                            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "HomeworkDock");
                    }
                    Directory.CreateDirectory(dir);
                    System.Diagnostics.Process.Start("explorer.exe", "\"" + dir + "\"");
                }
                catch { }
            };
            menu.Items.Add(openData);

            menu.Items.Add(new ToolStripSeparator());
            ToolStripMenuItem exit = new ToolStripMenuItem("退出程序");
            exit.Click += delegate { SafeInvoke(_exitAction); };
            menu.Items.Add(exit);

            menu.Opening += delegate
            {
                bool enabled = false;
                try { enabled = _getAutostart(); }
                catch { }
                _autostartItem.Checked = enabled;
            };
            return menu;
        }

        private static Icon LoadAppIcon()
        {
            try
            {
                return AppHost.LoadIcon();
            }
            catch
            {
                try { return (Icon)SystemIcons.Application.Clone(); }
                catch { return null; }
            }
        }

        private void SafeInvoke(Action action)
        {
            if (action == null) return;
            try { action(); }
            catch { }
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            try
            {
                if (_notifyIcon != null)
                {
                    _notifyIcon.Visible = false;
                    _notifyIcon.Dispose();
                    _notifyIcon = null;
                }
                if (_menu != null)
                {
                    _menu.Dispose();
                    _menu = null;
                }
                if (_icon != null && _icon != SystemIcons.Application)
                {
                    _icon.Dispose();
                    _icon = null;
                }
            }
            catch { }
        }
    }
}
