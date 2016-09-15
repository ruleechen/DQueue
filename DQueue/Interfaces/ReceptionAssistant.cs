using System.Collections.Generic;
using System.Threading;

namespace DQueue.Interfaces
{
    public class ReceptionAssistant
    {
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
    }

    public class ReceptionAssistant<TMessage> : ReceptionAssistant
    {
        public string QueueName { get; private set; }
        public string ProcessingQueueName { get; private set; }
        public CancellationToken Cancellation { get; private set; }

        public object DequeueLocker { get; private set; }
        public object PoolingLocker { get; private set; }

        public List<ReceptionContext<TMessage>> Pool { get; private set; }
        public bool IsDispatchingPool { get; set; }
        public CancellationTokenSource DelayCancellation { get; set; }

        public ReceptionAssistant(string hostId, string queueName, CancellationToken cancellation)
        {
            QueueName = queueName;
            ProcessingQueueName = (QueueName + string.Format(Constants.ProcessingQueueName, hostId));
            Cancellation = cancellation;

            DequeueLocker = GetLocker(QueueName + string.Format(Constants.DequeueLockerFlag, hostId));
            PoolingLocker = GetLocker(QueueName + string.Format(Constants.PoolingLockerFlag, hostId));

            Pool = new List<ReceptionContext<TMessage>>();
            IsDispatchingPool = false;
            DelayCancellation = null;
        }
    }
}
