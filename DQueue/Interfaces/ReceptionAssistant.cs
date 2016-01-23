using DQueue.Helpers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace DQueue.Interfaces
{
    public class ReceptionAssistant
    {
        #region static
        static readonly object _fallbackLocker;
        static readonly Dictionary<string, Action> _fallbackHandlers;

        static ReceptionAssistant()
        {
            _fallbackLocker = new object();
            _fallbackHandlers = new Dictionary<string, Action>();
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

                        return true;
                    }
                }
            }

            return false;
        }

        public static string GetQueueLocker(string associatedQueueName)
        {
            return associatedQueueName + "$locker$";
        }

        public static string GetProcessingQueueName(string associatedQueueName)
        {
            return associatedQueueName + "$processing$";
        }

        #endregion

        private object _locker;
        private Dictionary<int, List<Action>> _cancelHandlers;

        public ReceptionAssistant(int threads, string queueName, CancellationToken token)
        {
            _threads = threads;
            _queueName = queueName;
            _token = token;

            _locker = new object();
            _cancelHandlers = new Dictionary<int, List<Action>>();

            token.Register(() =>
            {
                var handlers = _cancelHandlers.ToList()
                    .OrderBy(x => x.Key);

                foreach (var order in handlers)
                {
                    foreach (var action in order.Value)
                    {
                        action();
                    }

                    Thread.Sleep(100);
                }

                _cancelHandlers.Clear();
            });
        }

        private int _threads;
        public int Threads
        {
            get
            {
                return _threads;
            }
        }

        private string _queueName;
        public string QueueName
        {
            get
            {
                return _queueName;
            }
        }

        private string _processingQueueName;
        public string ProcessingQueueName
        {
            get
            {
                return _processingQueueName ?? (_processingQueueName = GetProcessingQueueName(_queueName));
            }
        }

        private string _queueLocker;
        public string QueueLocker
        {
            get
            {
                return _queueLocker ?? (_queueLocker = GetQueueLocker(_queueName));
            }
        }

        private CancellationToken _token;
        public CancellationToken Token
        {
            get
            {
                return _token;
            }
        }

        private int _countExecuteFirstOne;
        public void ExecuteFirstOne(Action action)
        {
            if (action != null)
            {
                lock (_locker)
                {
                    _countExecuteFirstOne++;

                    if (_countExecuteFirstOne == 1)
                    {
                        action();
                    }
                }
            }
        }

        private int _countExecuteLastOne;
        public void ExecuteLastOne(Action action)
        {
            if (action != null)
            {
                lock (_locker)
                {
                    _countExecuteLastOne++;

                    if (_countExecuteLastOne == _threads)
                    {
                        action();
                    }
                }
            }
        }

        public void RegisterFallback(Action action)
        {
            var register = RegisterFallback(_queueName, action);
            if (register)
            {
                action();

                RegisterCancel(int.MaxValue, true, () =>
                {
                    lock (_fallbackLocker)
                    {
                        action();
                        _fallbackHandlers.Remove(_queueName);
                    }
                });
            }
        }

        public void RegisterCancel(int order, bool exclusive, Action action)
        {
            if (!_cancelHandlers.ContainsKey(order))
            {
                lock (_locker)
                {
                    if (!_cancelHandlers.ContainsKey(order))
                    {
                        _cancelHandlers.Add(order, new List<Action>());
                    }
                }
            }

            var actions = _cancelHandlers[order];

            lock (_locker)
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
