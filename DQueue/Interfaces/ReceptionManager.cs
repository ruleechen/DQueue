using DQueue.Helpers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace DQueue.Interfaces
{
    public class ReceptionManager
    {
        #region static
        static readonly object _fallbackLocker;
        static readonly Dictionary<string, Action> _fallbackHandlers;

        static readonly object _queueLock;
        static readonly Dictionary<string, object> _queueLockers;

        static ReceptionManager()
        {
            _fallbackLocker = new object();
            _fallbackHandlers = new Dictionary<string, Action>();

            _queueLock = new object();
            _queueLockers = new Dictionary<string, object>();
        }

        public static bool RegisterFallback(string queueName, Action action)
        {
            if (!_fallbackHandlers.ContainsKey(queueName))
            {
                lock (_fallbackLocker)
                {
                    if (!_fallbackHandlers.ContainsKey(queueName))
                    {
                        _fallbackHandlers.Add(queueName, action);

                        action();

                        return true;
                    }
                }
            }

            return false;
        }

        public static object GetQueueLocker(string queueName)
        {
            if (!_queueLockers.ContainsKey(queueName))
            {
                lock (_queueLock)
                {
                    if (!_queueLockers.ContainsKey(queueName))
                    {
                        _queueLockers.Add(queueName, new object());
                    }
                }
            }

            return _queueLockers[queueName];
        }
        #endregion

        private string _queueName;
        private object _cancelLocker;
        private CancellationToken _token;
        private Dictionary<int, List<Action>> _cancelHandlers;

        public ReceptionManager(string queueName, CancellationToken token)
        {
            _queueName = queueName;
            _token = token;

            _cancelLocker = new object();
            _cancelHandlers = new Dictionary<int, List<Action>>();

            token.Register(() =>
            {
                var handlers = _cancelHandlers.ToList()
                    .OrderBy(x => x.Key);

                foreach (var level in handlers)
                {
                    foreach (var action in level.Value)
                    {
                        action();
                    }

                    Thread.Sleep(100);
                }

                _cancelHandlers.Clear();
            });
        }

        public string QueueName
        {
            get
            {
                return _queueName;
            }
        }

        public string ProcessingQueueName
        {
            get
            {
                return QueueNameGenerator.GetProcessingQueueName(_queueName);
            }
        }

        public CancellationToken Token
        {
            get
            {
                return _token;
            }
        }

        public object QueueLocker()
        {
            return GetQueueLocker(_queueName);
        }

        public void OnFallback(Action action)
        {
            var register = RegisterFallback(_queueName, action);
            if (register)
            {
                OnCancel(int.MaxValue, true, () =>
                {
                    lock (_fallbackLocker)
                    {
                        action();
                        _fallbackHandlers.Remove(_queueName);
                    }
                });
            }
        }

        public void OnCancel(int execLevel, bool exclusive, Action action)
        {
            if (!_cancelHandlers.ContainsKey(execLevel))
            {
                lock (_cancelLocker)
                {
                    if (!_cancelHandlers.ContainsKey(execLevel))
                    {
                        _cancelHandlers.Add(execLevel, new List<Action>());
                    }
                }
            }

            var actions = _cancelHandlers[execLevel];

            lock (_cancelLocker)
            {
                if (exclusive)
                {
                    actions.Clear();
                }

                actions.Add(action);
            }
        }
    }
}
