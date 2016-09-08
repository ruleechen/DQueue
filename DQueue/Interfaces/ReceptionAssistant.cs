using System.Collections.Generic;
using System.Threading;

namespace DQueue.Interfaces
{
    public class ReceptionAssistant
    {
        #region static
        static readonly object _lockersLock;
        static readonly Dictionary<string, object> _lockers;

        static ReceptionAssistant()
        {
            _lockersLock = new object();
            _lockers = new Dictionary<string, object>();
        }

        public static object GetLocker(string key)
        {
            if (!_lockers.ContainsKey(key))
            {
                lock (_lockersLock)
                {
                    if (!_lockers.ContainsKey(key))
                    {
                        _lockers.Add(key, new object());
                    }
                }
            }

            return _lockers[key];
        }
        #endregion

        public string QueueName { get; private set; }
        public string ProcessingQueueName { get; private set; }

        public object MonitorLocker { get; private set; }
        public CancellationToken Cancellation { get; private set; }

        public ReceptionAssistant(string queueName, CancellationToken cancellation)
        {
            QueueName = queueName;
            ProcessingQueueName = (QueueName + Constants.ProcessingQueueName);

            Cancellation = cancellation;
            MonitorLocker = GetLocker(QueueName + Constants.MonitorLockerFlag);
        }
    }
}
