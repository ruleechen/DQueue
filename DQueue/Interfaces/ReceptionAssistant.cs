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
        public readonly static string Flag_ProcessingQueueName = "-$Processing$";
        public readonly static string Flag_QueueLocker = "-$QueueLocker$";
        public readonly static string Flag_MonitorLocker = "-$MonitorLocker$";

        static readonly object _lockersLock;
        static readonly Dictionary<string, object> _lockers;

        static ReceptionAssistant()
        {
            _lockersLock = new object();
            _lockers = new Dictionary<string, object>();
        }

        public static object GetLocker(string key, string flag)
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

        private object _apisLocker;
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
                return _processingQueueName ?? (_processingQueueName = _queueName + Flag_ProcessingQueueName);
            }
        }

        private object _queueLocker;
        public object QueueLocker
        {
            get
            {
                return _queueLocker ?? (_queueLocker = GetLocker(_queueName, Flag_QueueLocker));
            }
        }

        private object _monitorLocker;
        public object MonitorLocker
        {
            get
            {
                return _monitorLocker ?? (_monitorLocker = GetLocker(_queueName, Flag_MonitorLocker));
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

        private Action _fallbackAction;
        public void RegisterFallback(Action action)
        {
            if (_fallbackAction == null && action != null)
            {
                lock (_apisLocker)
                {
                    if (_fallbackAction == null)
                    {
                        _fallbackAction = action;
                        _fallbackAction();

                        RegisterCancel(int.MaxValue, true, () =>
                        {
                            _fallbackAction();
                            _fallbackAction = null;
                        });
                    }
                }
            }
        }

        private Dictionary<int, List<Action>> _cancelActions;
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
