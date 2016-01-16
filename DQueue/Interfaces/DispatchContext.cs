using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace DQueue.Interfaces
{
    public class DispatchContext
    {
        private Action<DispatchContext, DispatchStatus> _action;
        private CancellationToken _token;
        private List<Exception> _exceptions;
        private object _exceptionsLocker;

        public DispatchContext(CancellationToken token, Action<DispatchContext, DispatchStatus> action)
        {
            _token = token;
            _action = action;
            _exceptions = new List<Exception>();
            _exceptionsLocker = new object();
        }

        public CancellationToken Token
        {
            get
            {
                return _token;
            }
        }

        public void Complete()
        {
            _action(this, DispatchStatus.Complete);
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
    }
}
