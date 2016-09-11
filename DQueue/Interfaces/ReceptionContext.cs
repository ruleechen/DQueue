using System;

namespace DQueue.Interfaces
{
    public class ReceptionContext<TMessage>
    {
        private Action<ReceptionContext<TMessage>, ReceptionStatus> _action;

        public ReceptionContext(TMessage message, ReceptionAssistant<TMessage> assistant, Action<ReceptionContext<TMessage>, ReceptionStatus> action)
        {
            Message = message;
            Assistant = assistant;
            _action = action;
        }

        public TMessage Message { get; private set; }
        public ReceptionAssistant<TMessage> Assistant { get; private set; }

        public void Success()
        {
            _action(this, ReceptionStatus.Completed);
        }

        public void Timeout()
        {
            if (Constants.RetryOnTimeout)
            {
                _action(this, ReceptionStatus.Retry);
            }
            else
            {
                _action(this, ReceptionStatus.Completed);
            }
        }

        public void Withdraw()
        {
            _action(this, ReceptionStatus.Withdraw);
        }
    }
}
