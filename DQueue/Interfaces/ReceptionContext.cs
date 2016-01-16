using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace DQueue.Interfaces
{
    public class ReceptionContext
    {
        private Action<ReceptionContext, ReceptionStatus> _action;

        public ReceptionContext(Action<ReceptionContext, ReceptionStatus> action)
        {
            _action = action;
        }

        public void Success()
        {
            _action(this, ReceptionStatus.Success);
        }

        public void Withdraw()
        {
            _action(this, ReceptionStatus.Withdraw);
        }
    }
}
