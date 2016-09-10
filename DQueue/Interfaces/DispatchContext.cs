using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading;

namespace DQueue.Interfaces
{
    public class DispatchContext<TMessage> : IDisposable
    {
        private IDictionary _items;

        private TMessage _message;
        private Action<DispatchContext<TMessage>, DispatchStatus> _action;

        private List<Exception> _exceptions;
        private object _exceptionsLocker;

        internal object Locker { get; private set; }
        internal CancellationTokenSource OwnedCancellation { get; private set; }
        internal CancellationTokenSource LinkedCancellation { get; private set; }

        public DispatchContext(TMessage message, TimeSpan dispatchTimeout, CancellationTokenSource appCancellation, Action<DispatchContext<TMessage>, DispatchStatus> action)
        {
            _items = Hashtable.Synchronized(new Hashtable()); // thread safe

            _message = message;
            _action = action;

            _exceptions = new List<Exception>();
            _exceptionsLocker = new object();

            Locker = new object();
            OwnedCancellation = new CancellationTokenSource();
            LinkedCancellation = CancellationTokenSource.CreateLinkedTokenSource(
                appCancellation.Token,
                OwnedCancellation.Token,
                new CancellationTokenSource(dispatchTimeout).Token);
        }

        public TMessage Message
        {
            get
            {
                return _message;
            }
        }

        public CancellationToken Cancellation
        {
            get
            {
                return LinkedCancellation.Token;
            }
        }

        public IDictionary Items
        {
            get
            {
                return _items;
            }
        }

        public void GotoComplete()
        {
            _action(this, DispatchStatus.Complete);
        }

        public void GotoTimeout()
        {
            _action(this, DispatchStatus.Timeout);
        }

        public void LogException(Exception ex)
        {
            lock (_exceptionsLocker)
            {
                _exceptions.Add(ex);
            }
        }

        public IEnumerable<Exception> Exceptions
        {
            get
            {
                return _exceptions;
            }
        }

        public void Dispose()
        {
            if (!OwnedCancellation.IsCancellationRequested)
            {
                OwnedCancellation.Cancel();
            }
        }
    }
}
