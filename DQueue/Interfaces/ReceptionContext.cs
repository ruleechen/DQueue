using System;

namespace DQueue.Interfaces
{
    public class ReceptionContext<TMessage>
    {
        private TMessage _message;
        private Action<ReceptionContext<TMessage>, ReceptionStatus> _action;

        public ReceptionContext(TMessage message, Action<ReceptionContext<TMessage>, ReceptionStatus> action)
        {
            _message = message;
            _action = action;
        }

        public TMessage Message
        {
            get
            {
                return _message;
            }
        }

        public void Success()
        {
            _action(this, ReceptionStatus.Complete);
        }

        public void Withdraw()
        {
            _action(this, ReceptionStatus.Withdraw);
        }
    }
}
