using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading;

namespace DQueue.Interfaces
{
    public class DispatchContext<TMessage>
    {
        private IDictionary _items;

        private TMessage _message;
        private CancellationToken _token;
        private Action<DispatchContext<TMessage>, DispatchStatus> _action;

        private List<Exception> _exceptions;
        private object _exceptionsLocker;

        public DispatchContext(TMessage message, CancellationToken token, Action<DispatchContext<TMessage>, DispatchStatus> action)
        {
            _items = Hashtable.Synchronized(new Hashtable()); // thread safe

            _message = message;
            _token = token;
            _action = action;

            _exceptions = new List<Exception>();
            _exceptionsLocker = new object();
        }

        public TMessage Message
        {
            get
            {
                return _message;
            }
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
            _action(this, DispatchStatus.Complete);
        }

        public void LogException(Exception ex)
        {
            if (!_token.IsCancellationRequested)
            {
                lock (_exceptionsLocker)
                {
                    if (!_token.IsCancellationRequested)
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
