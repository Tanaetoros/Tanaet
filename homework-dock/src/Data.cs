using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Text;

namespace HomeworkDock
{
    public static class Priority
    {
        public const int Low = 0;
        public const int Medium = 1;
        public const int High = 2;

        public static string Label(int value)
        {
            switch (value)
            {
                case High: return "高";
                case Medium: return "中";
                default: return "低";
            }
        }

        public static string LongLabel(int value)
        {
            switch (value)
            {
                case High: return "高优先";
                case Medium: return "中优先";
                default: return "低优先";
            }
        }

        public static int Next(int value)
        {
            // High -> Medium -> Low -> High
            if (value == High) return Medium;
            if (value == Medium) return Low;
            return High;
        }

        public static int Clamp(int value)
        {
            if (value < Low) return Low;
            if (value > High) return High;
            return value;
        }
    }

    public static class DateUtil
    {
        public const string StorageFormat = "yyyy-MM-dd";
        private static readonly CultureInfo Zh = CultureInfo.GetCultureInfo("zh-CN");
        private static readonly string[] WeekNames = new string[] { "日", "一", "二", "三", "四", "五", "六" };

        public static string Key(DateTime date)
        {
            return date.ToString(StorageFormat, CultureInfo.InvariantCulture);
        }

        public static DateTime? ParseKey(string value)
        {
            if (string.IsNullOrEmpty(value)) return null;
            DateTime date;
            if (DateTime.TryParseExact(value, StorageFormat, CultureInfo.InvariantCulture,
                DateTimeStyles.None, out date))
            {
                return date.Date;
            }
            if (DateTime.TryParse(value, Zh, DateTimeStyles.None, out date))
            {
                return date.Date;
            }
            return null;
        }

        public static string HeaderDate(DateTime date)
        {
            return date.ToString("M月d日", Zh) + " · 星期" + WeekNames[(int)date.DayOfWeek];
        }

        public static string ShortDate(DateTime? date)
        {
            if (!date.HasValue) return "";
            DateTime today = DateTime.Today;
            int diff = (date.Value.Date - today).Days;
            if (diff == 0) return "今天";
            if (diff == 1) return "明天";
            if (diff == 2) return "后天";
            if (date.Value.Year == today.Year) return date.Value.ToString("M月d日", Zh);
            return date.Value.ToString("yyyy年M月d日", Zh);
        }

        public static string DueLabel(DateTime? date)
        {
            if (!date.HasValue) return "";
            DateTime today = DateTime.Today;
            int diff = (date.Value.Date - today).Days;
            if (diff < 0) return "逾期 " + (-diff).ToString(CultureInfo.InvariantCulture) + " 天";
            if (diff == 0) return "今天截止";
            if (diff == 1) return "明天截止";
            if (diff == 2) return "后天截止";
            if (diff <= 7)
            {
                return date.Value.ToString("M月d日", Zh) + " · 周" + WeekNames[(int)date.Value.DayOfWeek];
            }
            if (date.Value.Year == today.Year) return date.Value.ToString("M月d日", Zh);
            return date.Value.ToString("yyyy年M月d日", Zh);
        }

        public static string TodayKey()
        {
            return Key(DateTime.Today);
        }
    }

    [DataContract]
    public class TaskItem
    {
        [DataMember(Order = 1)] public string Id;
        [DataMember(Order = 2)] public string Title;
        [DataMember(Order = 3)] public int Priority;
        [DataMember(Order = 4)] public string Due;
        [DataMember(Order = 5)] public bool Done;
        [DataMember(Order = 6)] public string CreatedAt;
        [DataMember(Order = 7)] public string CompletedAt;

        public static TaskItem Create(string title, int priority, DateTime? due)
        {
            TaskItem task = new TaskItem();
            task.Id = Guid.NewGuid().ToString("N");
            task.Title = title;
            task.Priority = HomeworkDock.Priority.Clamp(priority);
            task.Due = due.HasValue ? DateUtil.Key(due.Value) : null;
            task.Done = false;
            task.CreatedAt = DateTime.Now.ToString("o", CultureInfo.InvariantCulture);
            task.CompletedAt = null;
            return task;
        }

        public DateTime? DueDate
        {
            get { return DateUtil.ParseKey(this.Due); }
        }

        public DateTime CreatedDate
        {
            get
            {
                DateTime value;
                if (DateTime.TryParse(this.CreatedAt, CultureInfo.InvariantCulture,
                    DateTimeStyles.RoundtripKind, out value))
                {
                    return value;
                }
                return DateTime.MinValue;
            }
        }

        public DateTime CompletedDate
        {
            get
            {
                DateTime value;
                if (DateTime.TryParse(this.CompletedAt, CultureInfo.InvariantCulture,
                    DateTimeStyles.RoundtripKind, out value))
                {
                    return value;
                }
                return DateTime.MinValue;
            }
        }

        public string DueDisplay
        {
            get { return DateUtil.DueLabel(this.DueDate); }
        }

        public bool IsOverdue
        {
            get
            {
                DateTime? due = this.DueDate;
                return !this.Done && due.HasValue && due.Value.Date < DateTime.Today;
            }
        }

        public bool IsDueToday
        {
            get
            {
                DateTime? due = this.DueDate;
                return !this.Done && due.HasValue && due.Value.Date == DateTime.Today;
            }
        }

        public void Normalize()
        {
            if (this.Id == null) this.Id = Guid.NewGuid().ToString("N");
            if (this.Title == null) this.Title = "";
            this.Title = this.Title.Trim();
            this.Priority = HomeworkDock.Priority.Clamp(this.Priority);
            DateTime? due = DateUtil.ParseKey(this.Due);
            this.Due = due.HasValue ? DateUtil.Key(due.Value) : null;
            if (this.CreatedAt == null)
            {
                this.CreatedAt = DateTime.Now.ToString("o", CultureInfo.InvariantCulture);
            }
            if (this.Done && this.CompletedAt == null)
            {
                this.CompletedAt = DateTime.Now.ToString("o", CultureInfo.InvariantCulture);
            }
            if (!this.Done) this.CompletedAt = null;
        }
    }

    [DataContract]
    public class AppSettings
    {
        [DataMember(Order = 1)] public bool Topmost = true;
        [DataMember(Order = 2)] public bool Autostart = true;
    }

    [DataContract]
    public class AppState
    {
        [DataMember(Order = 1)] public int SchemaVersion = 2;
        [DataMember(Order = 2)] public List<TaskItem> Tasks = new List<TaskItem>();
        [DataMember(Order = 3)] public AppSettings Settings = new AppSettings();
        [DataMember(Order = 4)] public string DeliveredDate = "";
        [DataMember(Order = 5)] public int DeliveredCount = 0;
        [DataMember(Order = 6)] public bool HasWindowPosition = false;
        [DataMember(Order = 7)] public double WindowLeft = 0;
        [DataMember(Order = 8)] public double WindowTop = 0;
        [DataMember(Order = 9)] public double WindowHeight = 0;
        [DataMember(Order = 10)] public bool FocusMode = true;
        [DataMember(Order = 11)] public bool ComposerExpanded = false;

        public void Normalize()
        {
            bool upgradeFromV1 = this.SchemaVersion < 2;
            this.SchemaVersion = 2;
            if (upgradeFromV1) this.FocusMode = true;
            if (this.Tasks == null) this.Tasks = new List<TaskItem>();
            if (this.Settings == null) this.Settings = new AppSettings();
            if (double.IsNaN(this.WindowHeight) || double.IsInfinity(this.WindowHeight) || this.WindowHeight < 0)
            {
                this.WindowHeight = 0;
            }
            List<TaskItem> clean = new List<TaskItem>();
            for (int i = 0; i < this.Tasks.Count; i++)
            {
                TaskItem task = this.Tasks[i];
                if (task == null) continue;
                task.Normalize();
                if (task.Title.Length == 0) continue;
                clean.Add(task);
            }
            this.Tasks = clean;
            RefreshDeliveryCounter();
        }

        public bool RefreshDeliveryCounter()
        {
            string today = DateUtil.TodayKey();
            if (!string.Equals(this.DeliveredDate, today, StringComparison.Ordinal))
            {
                this.DeliveredDate = today;
                this.DeliveredCount = 0;
                return true;
            }
            return false;
        }

        public int PendingCount
        {
            get
            {
                int count = 0;
                for (int i = 0; i < this.Tasks.Count; i++)
                {
                    if (!this.Tasks[i].Done) count++;
                }
                return count;
            }
        }

        public int DoneCount
        {
            get
            {
                int count = 0;
                for (int i = 0; i < this.Tasks.Count; i++)
                {
                    if (this.Tasks[i].Done) count++;
                }
                return count;
            }
        }

        public int HighPendingCount
        {
            get
            {
                int count = 0;
                for (int i = 0; i < this.Tasks.Count; i++)
                {
                    TaskItem task = this.Tasks[i];
                    if (!task.Done && task.Priority == HomeworkDock.Priority.High) count++;
                }
                return count;
            }
        }

        public int HighCount
        {
            get
            {
                int count = 0;
                for (int i = 0; i < this.Tasks.Count; i++)
                {
                    if (this.Tasks[i].Priority == HomeworkDock.Priority.High) count++;
                }
                return count;
            }
        }

        public void RecordDelivery()
        {
            string today = DateUtil.TodayKey();
            if (!string.Equals(this.DeliveredDate, today, StringComparison.Ordinal))
            {
                this.DeliveredDate = today;
                this.DeliveredCount = 0;
            }
            this.DeliveredCount++;
        }

        public void UndoDeliveryRecord()
        {
            if (this.DeliveredCount > 0) this.DeliveredCount--;
        }
    }

    public class StateStore
    {
        private readonly string _dataDir;
        private readonly string _statePath;
        private readonly string _logPath;

        public string DataDir { get { return _dataDir; } }
        public string StatePath { get { return _statePath; } }
        public string LogPath { get { return _logPath; } }
        public string LoadWarning { get; private set; }
        public string InitWarning { get; private set; }
        public bool WasFirstRun { get; private set; }

        public StateStore()
        {
            string overrideDir = Environment.GetEnvironmentVariable("HOMEWORKDOCK_DATA_DIR");
            List<string> candidates = new List<string>();
            if (!string.IsNullOrEmpty(overrideDir)) candidates.Add(overrideDir);
            candidates.Add(Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "HomeworkDock"));
            candidates.Add(Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "HomeworkDock"));
            candidates.Add(Path.Combine(Path.GetTempPath(), "HomeworkDock"));
            candidates.Add(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "data"));

            Exception lastError = null;
            List<string> tried = new List<string>();
            foreach (string candidate in candidates)
            {
                if (string.IsNullOrEmpty(candidate)) continue;
                string full;
                try { full = Path.GetFullPath(candidate); }
                catch { continue; }
                if (tried.Contains(full)) continue;
                tried.Add(full);
                try
                {
                    Directory.CreateDirectory(full);
                    _dataDir = full;
                    break;
                }
                catch (Exception ex)
                {
                    lastError = ex;
                }
            }

            if (string.IsNullOrEmpty(_dataDir))
            {
                _dataDir = Path.Combine(Path.GetTempPath(), "HomeworkDock");
                this.InitWarning = "无法创建数据文件夹，作业可能无法保存。" +
                    (lastError == null ? "" : "\n" + lastError.Message);
            }

            _statePath = Path.Combine(_dataDir, "state.json");
            _logPath = Path.Combine(_dataDir, "error.log");
        }

        public AppState Load()
        {
            this.LoadWarning = null;
            this.WasFirstRun = !File.Exists(_statePath);
            if (this.WasFirstRun)
            {
                AppState first = new AppState();
                first.Normalize();
                return first;
            }

            try
            {
                byte[] bytes = File.ReadAllBytes(_statePath);
                int offset = 0;
                if (bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF)
                {
                    offset = 3;
                }
                using (MemoryStream stream = new MemoryStream(bytes, offset, bytes.Length - offset))
                {
                    DataContractJsonSerializer serializer = new DataContractJsonSerializer(typeof(AppState));
                    AppState state = (AppState)serializer.ReadObject(stream);
                    if (state == null) throw new InvalidDataException("state.json 为空。");
                    state.Normalize();
                    return state;
                }
            }
            catch (Exception ex)
            {
                this.LoadWarning = "数据文件读取失败，已备份原文件并创建空白清单。";
                this.Log("Load failed: " + ex);
                try
                {
                    string backup = Path.Combine(_dataDir,
                        "state.corrupt-" + DateTime.Now.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture) + ".json");
                    File.Copy(_statePath, backup, true);
                    this.LoadWarning += " 备份：" + Path.GetFileName(backup);
                }
                catch
                {
                }
                AppState empty = new AppState();
                empty.Normalize();
                return empty;
            }
        }

        public void Save(AppState state)
        {
            if (state == null) throw new ArgumentNullException("state");
            string temp = _statePath + ".tmp";
            try
            {
                using (FileStream stream = new FileStream(temp, FileMode.Create, FileAccess.Write, FileShare.None))
                {
                    DataContractJsonSerializer serializer = new DataContractJsonSerializer(typeof(AppState));
                    serializer.WriteObject(stream, state);
                    stream.Flush(true);
                }

                if (File.Exists(_statePath))
                {
                    try
                    {
                        File.Replace(temp, _statePath, null);
                    }
                    catch
                    {
                        File.Copy(temp, _statePath, true);
                        File.Delete(temp);
                    }
                }
                else
                {
                    File.Move(temp, _statePath);
                }
            }
            catch (Exception ex)
            {
                this.Log("Save failed: " + ex);
                try
                {
                    if (File.Exists(temp)) File.Delete(temp);
                }
                catch
                {
                }
                throw;
            }
        }

        public void Log(string message)
        {
            try
            {
                File.AppendAllText(_logPath,
                    DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture) + "  " + message + Environment.NewLine,
                    new UTF8Encoding(false));
            }
            catch
            {
            }
        }
    }
}
