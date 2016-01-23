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
        static readonly object _lockersLock;
        static readonly Dictionary<string, object> _lockers;

        static readonly object _actionsLock;
        static readonly Dictionary<string, Action> _actions;

        static ReceptionAssistant()
        {
            _lockersLock = new object();
            _lockers = new Dictionary<string, object>();

            _actionsLock = new object();
            _actions = new Dictionary<string, Action>();
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

        public static bool RegisterAction(string key, Action action)
        {
            if (!_actions.ContainsKey(key))
            {
                lock (_actionsLock)
                {
                    if (!_actions.ContainsKey(key))
                    {
                        _actions.Add(key, action);

                        return true;
                    }
                }
            }

            return false;
        }

        public static string GetProcessingQueueName(string associatedQueueName)
        {
            return associatedQueueName + "$processing$";
        }
        #endregion

        private object _apisLocker;
        private Dictionary<int, List<Action>> _cancelActions;

        public ReceptionAssistant(int threads, string queueName, CancellationToken token)
        {
            _threads = threads;
            _queueName = queueName;
            _token = token;

            _apisLocker = new object();
            _cancelActions = new Dictionary<int, List<Action>>();

            token.Register(() =>
            {
                var actions = _cancelActions.ToList()
                    .OrderBy(x => x.Key);

                foreach (var order in actions)
                {
                    foreach (var action in order.Value)
                    {
                        action();
                    }

                    Thread.Sleep(100);
                }

                _cancelActions.Clear();
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

        private object _queueLocker;
        public object QueueLocker
        {
            get
            {
                return _queueLocker ?? (_queueLocker = GetLocker(_queueName + "$QueueLocker$"));
            }
        }

        private object _monitorLocker;
        public object MonitorLocker
        {
            get
            {
                return _monitorLocker ?? (_monitorLocker = GetLocker(_queueName + "$MonitorLocker$"));
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
                lock (_apisLocker)
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
                lock (_apisLocker)
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
            var register = RegisterAction(_queueName, action);
            if (register)
            {
                action();

                RegisterCancel(int.MaxValue, true, () =>
                {
                    lock (_actionsLock)
                    {
                        action();
                        _actions.Remove(_queueName);
                    }
                });
            }
        }

        public void RegisterCancel(int order, bool exclusive, Action action)
        {
            if (!_cancelActions.ContainsKey(order))
            {
                lock (_apisLocker)
                {
                    if (!_cancelActions.ContainsKey(order))
                    {
                        _cancelActions.Add(order, new List<Action>());
                    }
                }
            }

            var actions = _cancelActions[order];

            lock (_apisLocker)
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
