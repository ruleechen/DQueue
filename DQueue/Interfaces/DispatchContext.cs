using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading;

namespace DQueue.Interfaces
{
    public class DispatchContext<TMessage> : IDisposable
    {
        private Action<DispatchContext<TMessage>, DispatchStatus> _action;
        private List<Exception> _exceptions;
        private object _exceptionsLocker;

        internal object Locker { get; private set; }
        internal CancellationTokenSource OwnedCancellation { get; private set; }
        internal CancellationTokenSource LinkedCancellation { get; private set; }

        public DispatchContext(
            TMessage message,
            TimeSpan dispatchTimeout,
            CancellationTokenSource appCancellation,
            Action<DispatchContext<TMessage>, DispatchStatus> action)
        {
            DispatchStatus = DispatchStatus.None;
            Items = Hashtable.Synchronized(new Hashtable()); // thread safe
            Message = message;
            _action = action;

            _exceptions = new List<Exception>();
            _exceptionsLocker = new object();

            Locker = new object();
            OwnedCancellation = new CancellationTokenSource();
            LinkedCancellation = CancellationTokenSource.CreateLinkedTokenSource(
                appCancellation.Token,
                OwnedCancellation.Token,
                new CancellationTokenSource(dispatchTimeout).Token
            );
        }

        public TMessage Message { get; private set; }
        public DispatchStatus DispatchStatus { get; private set; }
        public IEnumerable<Exception> Exceptions { get { return _exceptions; } }
        public IDictionary Items { get; private set; }

        public CancellationToken Cancellation
        {
            get
            {
                return LinkedCancellation.Token;
            }
        }

        private void EmitStatus(DispatchStatus status)
        {
            DispatchStatus = status;

            if (_action != null)
            {
                _action.Invoke(this, status);
            }
        }

        public void GotoComplete()
        {
            EmitStatus(DispatchStatus.Complete);
        }

        public void GotoTimeout()
        {
            EmitStatus(DispatchStatus.Timeout);
        }

        public void LogException(Exception ex)
        {
            lock (_exceptionsLocker)
            {
                _exceptions.Add(ex);
            }
        }

        /// <summary>
        /// https://stackoverflow.com/questions/6960520/when-to-dispose-cancellationtokensource
        /// </summary>
        public void Dispose()
        {
            if (LinkedCancellation != null)
            {
                if (!LinkedCancellation.IsCancellationRequested)
                {
                    LinkedCancellation.Cancel();
                }

                LinkedCancellation.Dispose();
                LinkedCancellation = null;
            }

            if (OwnedCancellation != null)
            {
                if (!OwnedCancellation.IsCancellationRequested)
                {
                    OwnedCancellation.Cancel();
                }

                OwnedCancellation.Dispose();
                OwnedCancellation = null;
            }

            if (_exceptions != null)
            {
                _exceptions.Clear();
                _exceptions = null;
            }

            if (Items != null)
            {
                Items.Clear();
                Items = null;
            }

            _action = null;
            _exceptionsLocker = null;

            Locker = null;
            Message = default(TMessage);
        }
    }
}
