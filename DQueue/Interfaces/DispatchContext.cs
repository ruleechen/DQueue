using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace DQueue.Interfaces
{
    public class DispatchContext
    {
        private IDictionary _items;

        private Action<DispatchContext, DispatchStatus> _action;
        private CancellationToken _token;
        private List<Exception> _exceptions;

        private bool _isCompleted;
        private object _exceptionsLocker;

        public DispatchContext(CancellationToken token, Action<DispatchContext, DispatchStatus> action)
        {
            _items = Hashtable.Synchronized(new Hashtable()); // thread safe

            _token = token;
            _action = action;
            _exceptions = new List<Exception>();

            _isCompleted = false;
            _exceptionsLocker = new object();
        }

        public CancellationToken Token
        {
            get
            {
                return _token;
            }
        }

        public IDictionary Items
        {
            get
            {
                return _items;
            }
        }

        public void Complete()
        {
            _isCompleted = true;
            _action(this, DispatchStatus.Complete);
        }

        public void LogException(Exception ex)
        {
            if (!_isCompleted)
            {
                lock (_exceptionsLocker)
                {
                    if (!_isCompleted)
                    {
                        _exceptions.Add(ex);
                    }
                }
            }
        }

        public IEnumerable<Exception> Exceptions
        {
            get
            {
                return _exceptions;
            }
        }
    }
}
