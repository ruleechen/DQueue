using System;

namespace DQueue.Interfaces
{
    public class ReceptionContext<TMessage>
    {
        private Action<ReceptionContext<TMessage>, ReceptionStatus> _action;

        public ReceptionContext(TMessage message, object rawMessage, ReceptionAssistant<TMessage> assistant, Action<ReceptionContext<TMessage>, ReceptionStatus> action)
        {
            Message = message;
            RawMessage = rawMessage;
            Assistant = assistant;
            _action = action;
        }

        public TMessage Message { get; private set; }
        public object RawMessage { get; private set; }
        public ReceptionAssistant<TMessage> Assistant { get; private set; }
        public Action OnDone { get; set; }

        private void EmitStatus(ReceptionStatus status)
        {
            if (_action != null)
            {
                _action.Invoke(this, status);
            }

            if (OnDone != null)
            {
                OnDone.Invoke();
            }
        }

        public void Success()
        {
            EmitStatus(ReceptionStatus.Completed);
        }

        public void Timeout()
        {
            if (Constants.RetryOnTimeout)
            {
                EmitStatus(ReceptionStatus.Retry);
            }
            else
            {
                EmitStatus(ReceptionStatus.Completed);
            }
        }

        public void Withdraw()
        {
            EmitStatus(ReceptionStatus.Withdraw);
        }
    }
}
