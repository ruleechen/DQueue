using System;

namespace DQueue.Interfaces
{
    public class ReceptionContext<TMessage>
    {
        private Action<ReceptionContext<TMessage>, DispatchStatus> _feedback;

        public ReceptionContext(
            TMessage message,
            object rawMessage,
            ReceptionAssistant<TMessage> assistant,
            Action<ReceptionContext<TMessage>, DispatchStatus> feedback)
        {
            Message = message;
            RawMessage = rawMessage;
            Assistant = assistant;
            _feedback = feedback;
        }

        public TMessage Message { get; private set; }
        public object RawMessage { get; private set; }
        public ReceptionAssistant<TMessage> Assistant { get; private set; }
        public Action OnDone { get; set; }

        private void EmitFeedback(DispatchStatus status)
        {
            if (_feedback != null)
            {
                _feedback.Invoke(this, status);
            }

            if (OnDone != null)
            {
                OnDone.Invoke();
            }
        }

        public void FeedbackSuccess()
        {
            EmitFeedback(DispatchStatus.Complete);
        }

        public void FeedbackTimeout()
        {
            if (Constants.RetryOnTimeout)
            {
                EmitFeedback(DispatchStatus.Timeout);
            }
            else
            {
                EmitFeedback(DispatchStatus.Complete);
            }
        }
    }
}
