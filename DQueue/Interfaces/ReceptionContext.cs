using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace DQueue.Interfaces
{
    public class ReceptionContext
    {
        private IQueueProvider _provider;

        public ReceptionContext(IQueueProvider provider)
        {
            if (provider == null)
            {
                throw new ArgumentNullException("provider", "The provider is required");
            }

            _provider = provider;
        }

        public void Suspend()
        {
            if (_provider == null)
            {
                throw new InvalidOperationException("The reception status has been specified");
            }

            _provider.ReceptionStatus = ReceptionStatus.Suspend;
            _provider = null;
        }

        public void Continue()
        {
            if (_provider == null)
            {
                throw new InvalidOperationException("The reception status has been specified");
            }

            _provider.ReceptionStatus = ReceptionStatus.Listen;
            _provider = null;
        }

        public void BreakOff()
        {
            if (_provider == null)
            {
                throw new InvalidOperationException("The reception status has been specified");
            }

            _provider.ReceptionStatus = ReceptionStatus.BreakOff;
            _provider = null;
        }
    }
}
