using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace DQueue.Interfaces
{
    public class CancellationPack
    {
        private object _locker;
        private CancellationToken _token;
        private List<Action> _lastActions;
        private CancellationTokenRegistration _registration;

        public CancellationPack(CancellationToken token)
        {
            _token = token;
            _locker = new object();
            _lastActions = new List<Action>();
            InitializeLastAction();
        }

        private void InitializeLastAction()
        {
            if (_registration != null)
            {
                _registration.Dispose();
            }

            _registration = _token.Register(() =>
            {
                foreach (var last in _lastActions)
                {
                    last();
                }
            });
        }

        public void Register(Action action)
        {
            lock (_locker)
            {
                _token.Register(action);
                InitializeLastAction();
            }
        }

        public void RegisterLast(Action action, bool exclusive = true)
        {
            lock (_locker)
            {
                if (exclusive)
                {
                    _lastActions.Clear();
                }

                _lastActions.Add(action);
            }
        }
    }
}
