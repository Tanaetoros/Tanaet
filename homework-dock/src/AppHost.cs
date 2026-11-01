using System;
using System.Drawing;
using System.IO;
using System.Reflection;

namespace HomeworkDock
{
    /// <summary>
    /// Describes how the app was started. The normal build is a DLL hosted by
    /// Windows PowerShell, because Smart App Control blocks freshly built,
    /// unsigned EXEs on some machines. A direct EXE build still works too.
    /// </summary>
    public static class AppHost
    {
        private static string AssemblyPathFromDisk
        {
            get
            {
                try
                {
                    return Assembly.GetExecutingAssembly().Location;
                }
                catch
                {
                    return "";
                }
            }
        }

        public static string InstallDir
        {
            get
            {
                // run.ps1 sets this before loading the DLL from memory.
                string home = Environment.GetEnvironmentVariable("HOMEWORKDOCK_HOME");
                if (!string.IsNullOrEmpty(home) && Directory.Exists(home))
                {
                    try { return Path.GetFullPath(home); }
                    catch { return home; }
                }

                string path = AssemblyPathFromDisk;
                if (string.IsNullOrEmpty(path)) return AppDomain.CurrentDomain.BaseDirectory;
                return Path.GetDirectoryName(path);
            }
        }

        public static string AssemblyPath
        {
            get
            {
                string path = AssemblyPathFromDisk;
                if (!string.IsNullOrEmpty(path)) return path;
                return Path.Combine(InstallDir, "HomeworkDock.dll");
            }
        }

        public static bool UsesPowerShellHost
        {
            get
            {
                return !string.Equals(Path.GetExtension(AssemblyPath), ".exe",
                    StringComparison.OrdinalIgnoreCase);
            }
        }

        public static string PowerShellPath
        {
            get
            {
                string windows = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
                return Path.Combine(windows, @"System32\WindowsPowerShell\v1.0\powershell.exe");
            }
        }

        public static string LauncherPath
        {
            get { return Path.Combine(InstallDir, "run.ps1"); }
        }

        public static string IconPath
        {
            get
            {
                string ico = Path.Combine(InstallDir, "app.ico");
                if (File.Exists(ico)) return ico;
                return AssemblyPath;
            }
        }

        public static string ShortcutTarget
        {
            get { return UsesPowerShellHost ? PowerShellPath : AssemblyPath; }
        }

        public static string BuildShortcutArguments(string extra)
        {
            if (!UsesPowerShellHost) return extra == null ? "" : extra;
            string arguments = "-NoProfile -WindowStyle Hidden -ExecutionPolicy Bypass -File \"" +
                LauncherPath + "\"";
            if (!string.IsNullOrEmpty(extra)) arguments += " " + extra;
            return arguments;
        }

        public static Icon LoadIcon()
        {
            try
            {
                string path = IconPath;
                if (File.Exists(path) &&
                    string.Equals(Path.GetExtension(path), ".ico", StringComparison.OrdinalIgnoreCase))
                {
                    return new Icon(path);
                }
            }
            catch
            {
            }

            try
            {
                string path = IconPath;
                if (File.Exists(path))
                {
                    using (Icon extracted = Icon.ExtractAssociatedIcon(path))
                    {
                        if (extracted != null) return (Icon)extracted.Clone();
                    }
                }
            }
            catch
            {
            }

            return (Icon)SystemIcons.Application.Clone();
        }
    }
}
