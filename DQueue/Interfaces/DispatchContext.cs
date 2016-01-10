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
        private Action<DispatchStatus> _action;
        private CancellationToken _token;

        public DispatchContext(CancellationToken token, Action<DispatchStatus> action)
        {
            _token = token;
            _action = action;
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
            _action(DispatchStatus.Complete);
        }
    }
}
