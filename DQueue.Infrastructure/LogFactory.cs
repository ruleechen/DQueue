using System;
using System.IO;
using System.Text;

namespace DQueue.Infrastructure
{
    public interface ILogger
    {
        void Error(string message, Exception ex);

        void Debug(string message);
    }

    public class LogFactory
    {
        static ILogger _log;
        static object _locker;

        static LogFactory()
        {
            _locker = new object();
        }

        public static ILogger GetLogger()
        {
            if (_log == null)
            {
                lock (_locker)
                {
                    if (_log == null)
                    {
                        _log = new Logger();
                    }
                }
            }

            return _log;
        }
    }

    public class Logger : ILogger
    {
        private string _filePath;

        public Logger()
        {
            var folder = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "AppLogs");
            if (!Directory.Exists(folder)) { Directory.CreateDirectory(folder); }
            _filePath = Path.Combine(folder, DateTime.Now.ToString("yyyyMMdd") + ".log");
        }

        public void Error(string message, Exception ex)
        {
            var sb = new StringBuilder();
            sb.Append("[Error]");
            sb.Append(DateTime.Now.ToString("yyyy-MM-dd hh:mm:ss.ffffff"));
            sb.Append(": ");
            sb.AppendLine(message);
            if (ex != null) { sb.AppendLine(ex.ToString()); }
            sb.AppendLine();

            WriteLog(sb.ToString());
        }

        public void Debug(string message)
        {
            var sb = new StringBuilder();
            sb.Append("[Debug]");
            sb.Append(DateTime.Now.ToString("yyyy-MM-dd hh:mm:ss.ffffff"));
            sb.Append(": ");
            if (!string.IsNullOrWhiteSpace(message)) { sb.AppendLine(message); }
            sb.AppendLine();

            WriteLog(sb.ToString());
        }

        private void WriteLog(string data)
        {
            using (var stream = new FileStream(_filePath, FileMode.Append))
            {
                using (var writer = new StreamWriter(stream))
                {
                    writer.Write(data);
                }
            }
        }
    }
}
