using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace DQueue.Interfaces
{
    public class ReceptionContext
    {
        private Action<ReceptionStatus> _action;

        public ReceptionContext(Action<ReceptionStatus> action)
        {
            if (action == null)
            {
                throw new ArgumentNullException("action", "The reception action is required");
            }

            _action = action;
        }

        public void Continue()
        {
            if (_action == null)
            {
                throw new InvalidOperationException("The reception status has been specified");
            }

            _action(ReceptionStatus.Listen);
            _action = null;
        }

        public void BreakOff()
        {
            if (_action == null)
            {
                throw new InvalidOperationException("The reception status has been specified");
            }

            _action(ReceptionStatus.BreakOff);
            _action = null;
        }
    }
}
