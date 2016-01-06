using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DQueue.Interfaces
{
    public class ReceptionContext
    {
        private Action<ReceptionStatus> _action;

        public ReceptionContext(Action<ReceptionStatus> action)
        {
            _action = action;
        }

        public void Complete()
        {
            if (_action != null)
            {
                _action(ReceptionStatus.Querying);
                _action = null;
            }
        }

        public void BreakOff()
        {
            if (_action != null)
            {
                _action(ReceptionStatus.BreakOff);
                _action = null;
            }
        }
    }
}
