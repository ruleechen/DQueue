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
            _provider = provider;
        }

        public ReceptionStatus Status
        {
            get
            {
                return _provider.ReceptionStatus;
            }
        }

        public void Suspend()
        {
            _provider.ReceptionStatus = ReceptionStatus.Suspend;
        }

        public void Continue()
        {
            _provider.ReceptionStatus = ReceptionStatus.Listen;
        }

        public void BreakOff()
        {
            _provider.ReceptionStatus = ReceptionStatus.BreakOff;
        }
    }
}
