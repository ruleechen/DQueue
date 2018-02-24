using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace DQueue.Infrastructure
{
    public interface ILogger
    {
        void Error(string title, Exception ex);
        void Error(string title, string message = null);
        void Debug(string title, string message = null);
        void Info(string title, string message = null);
    }

    public class LogFactory
    {
        static object _locker;
        static IDictionary<string, ILogger> _loggers;

        static LogFactory()
        {
            _locker = new object();
            _loggers = new Dictionary<string, ILogger>();
        }

        public static ILogger GetLogger(string fileName = null, bool isOverride = false)
        {
            if (fileName == null)
            {
                fileName = string.Empty;
            }

            if (!_loggers.ContainsKey(fileName))
            {
                lock (_locker)
                {
                    if (!_loggers.ContainsKey(fileName))
                    {
                        _loggers.Add(fileName, new Logger(fileName, isOverride));
                    }
                }
            }

            return _loggers[fileName];
        }
    }

    public class Logger : ILogger
    {
        private string _fileName;
        private bool _isOverride;

        public Logger(string fileName, bool isOverride = false)
        {
            _fileName = fileName;
            _isOverride = isOverride;
        }

        public void Error(string title, Exception ex)
        {
            Error(title, ex.ToString());
        }

        public void Error(string title, string message = null)
        {
            WriteLog(FormatMessage("[ERROR]", title, message).ToString());
        }

        public void Debug(string title, string message = null)
        {
            WriteLog(FormatMessage("[DEBUG]", title, message).ToString());
        }

        public void Info(string title, string message = null)
        {
            WriteLog(FormatMessage("[INFO]", title, message).ToString());
        }

        private static StringBuilder FormatMessage(string flag, string title, string message)
        {
            var sb = new StringBuilder();
            sb.Append(flag).Append(" ");
            sb.Append(DateTime.Now.ToString("yyyy-MM-dd hh:mm:ss.ffffff"));
            sb.Append(":");
            if (!string.IsNullOrEmpty(title)) { sb.Append(" ").Append(title); }
            if (!string.IsNullOrEmpty(message)) { sb.AppendLine().AppendLine(message); }
            sb.AppendLine();
            return sb;
        }

        private void WriteLog(string data)
        {
            var fullName = GetFileFullName();
            var mode = _isOverride ? FileMode.Create : FileMode.Append;

            using (var stream = new FileStream(fullName, mode))
            {
                using (var writer = new StreamWriter(stream))
                {
                    writer.Write(data);
                }
            }
        }

        private string GetFileFullName()
        {
            var todayName = GetTodayName();
            var name = string.IsNullOrWhiteSpace(_fileName) ? todayName : _fileName;
            var extension = Path.GetExtension(name);

            name = Path.GetFileNameWithoutExtension(name);
            extension = string.IsNullOrEmpty(extension) ? ".log" : extension;

            if (name != todayName && !_isOverride)
            { name = name.TrimEnd('-') + '-' + todayName; }

            var fullName = Path.Combine(DefaultLogFolder(), name + extension);
            return fullName;
        }

        public static string DefaultLogFolder()
        {
            var folder = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "AppLogs");
            if (!Directory.Exists(folder)) { Directory.CreateDirectory(folder); }
            return folder;
        }

        public static string GetTodayName()
        {
            return DateTime.Now.ToString("yyyyMMdd");
        }
    }
}
