using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DQueue.Interfaces
{
    public class ReceptionContext
    {
        private Action _action;

        public ReceptionContext(Action action)
        {
            _action = action;
        }

        public void Complete()
        {
            if (_action != null)
            {
                _action();
                _action = null;
            }
        }
    }
}
