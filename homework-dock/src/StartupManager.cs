using System;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;

namespace HomeworkDock
{
    public static class StartupManager
    {
        public const string ShortcutFileName = "作业清单.lnk";

        public static string StartupFolder
        {
            get
            {
                return Environment.GetFolderPath(Environment.SpecialFolder.Startup);
            }
        }

        public static string ShortcutPath
        {
            get { return Path.Combine(StartupFolder, ShortcutFileName); }
        }

        public static bool IsEnabled
        {
            get { return File.Exists(ShortcutPath); }
        }

        public static bool IsCurrentExecutableRegistered()
        {
            if (!IsEnabled) return false;
            try
            {
                string target = ReadShortcutTarget(ShortcutPath);
                string arguments = ReadShortcutArguments(ShortcutPath);
                if (!string.Equals(NormalizePath(target), NormalizePath(AppHost.ShortcutTarget),
                    StringComparison.OrdinalIgnoreCase))
                {
                    return false;
                }
                if (AppHost.UsesPowerShellHost)
                {
                    return arguments != null && arguments.IndexOf(AppHost.LauncherPath,
                        StringComparison.OrdinalIgnoreCase) >= 0;
                }
                return arguments != null && arguments.IndexOf("--startup",
                    StringComparison.OrdinalIgnoreCase) >= 0;
            }
            catch
            {
                return false;
            }
        }

        public static string CurrentExecutable
        {
            get { return AppHost.AssemblyPath; }
        }

        public static void SetEnabled(bool enabled)
        {
            if (enabled)
            {
                EnsureEnabled(true);
            }
            else
            {
                if (File.Exists(ShortcutPath)) File.Delete(ShortcutPath);
            }
        }

        public static bool EnsureEnabled(bool forceRewrite)
        {
            if (!forceRewrite && IsCurrentExecutableRegistered()) return true;
            Directory.CreateDirectory(StartupFolder);
            CreateShortcut(ShortcutPath, AppHost.ShortcutTarget,
                AppHost.BuildShortcutArguments("--startup"), AppHost.IconPath, AppHost.InstallDir);
            return IsCurrentExecutableRegistered();
        }

        public static string ReadShortcutTarget(string shortcutPath)
        {
            object shell = null;
            object shortcut = null;
            try
            {
                Type shellType = Type.GetTypeFromProgID("WScript.Shell");
                if (shellType == null) throw new InvalidOperationException("WScript.Shell 不可用。");
                shell = Activator.CreateInstance(shellType);
                shortcut = shellType.InvokeMember("CreateShortcut", BindingFlags.InvokeMethod, null,
                    shell, new object[] { shortcutPath });
                object target = shortcut.GetType().InvokeMember("TargetPath", BindingFlags.GetProperty,
                    null, shortcut, null);
                return target as string;
            }
            finally
            {
                ReleaseCom(shortcut);
                ReleaseCom(shell);
            }
        }

        public static string ReadShortcutArguments(string shortcutPath)
        {
            object shell = null;
            object shortcut = null;
            try
            {
                Type shellType = Type.GetTypeFromProgID("WScript.Shell");
                if (shellType == null) throw new InvalidOperationException("WScript.Shell 不可用。");
                shell = Activator.CreateInstance(shellType);
                shortcut = shellType.InvokeMember("CreateShortcut", BindingFlags.InvokeMethod, null,
                    shell, new object[] { shortcutPath });
                object value = shortcut.GetType().InvokeMember("Arguments", BindingFlags.GetProperty,
                    null, shortcut, null);
                return value as string;
            }
            finally
            {
                ReleaseCom(shortcut);
                ReleaseCom(shell);
            }
        }

        public static void CreateShortcut(string shortcutPath, string targetPath, string arguments,
            string iconPath, string workingDirectory)
        {
            object shell = null;
            object shortcut = null;
            try
            {
                Type shellType = Type.GetTypeFromProgID("WScript.Shell");
                if (shellType == null) throw new InvalidOperationException("WScript.Shell 不可用。");
                shell = Activator.CreateInstance(shellType);
                shortcut = shellType.InvokeMember("CreateShortcut", BindingFlags.InvokeMethod, null,
                    shell, new object[] { shortcutPath });
                Type shortcutType = shortcut.GetType();
                shortcutType.InvokeMember("TargetPath", BindingFlags.SetProperty, null, shortcut,
                    new object[] { targetPath });
                shortcutType.InvokeMember("Arguments", BindingFlags.SetProperty, null, shortcut,
                    new object[] { arguments == null ? "" : arguments });
                shortcutType.InvokeMember("WorkingDirectory", BindingFlags.SetProperty, null, shortcut,
                    new object[] { string.IsNullOrEmpty(workingDirectory)
                        ? Path.GetDirectoryName(targetPath) : workingDirectory });
                string icon = string.IsNullOrEmpty(iconPath) ? targetPath : iconPath;
                if (!string.Equals(Path.GetExtension(icon), ".ico", StringComparison.OrdinalIgnoreCase))
                {
                    icon += ",0";
                }
                shortcutType.InvokeMember("IconLocation", BindingFlags.SetProperty, null, shortcut,
                    new object[] { icon });
                shortcutType.InvokeMember("Description", BindingFlags.SetProperty, null, shortcut,
                    new object[] { "作业清单 · 桌面悬浮窗" });
                shortcutType.InvokeMember("WindowStyle", BindingFlags.SetProperty, null, shortcut,
                    new object[] { 1 });
                shortcutType.InvokeMember("Save", BindingFlags.InvokeMethod, null, shortcut, null);
            }
            finally
            {
                ReleaseCom(shortcut);
                ReleaseCom(shell);
            }
        }

        private static string NormalizePath(string path)
        {
            if (string.IsNullOrEmpty(path)) return "";
            try
            {
                return Path.GetFullPath(path).TrimEnd('\\');
            }
            catch
            {
                return path.TrimEnd('\\');
            }
        }

        private static void ReleaseCom(object value)
        {
            if (value != null && Marshal.IsComObject(value))
            {
                try { Marshal.ReleaseComObject(value); }
                catch { }
            }
        }
    }
}
