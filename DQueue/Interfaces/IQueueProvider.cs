using System;

namespace DQueue.Interfaces
{
    public interface IQueueProvider
    {
        bool ExistsMessage(string queueName, object message);

        void Enqueue(string queueName, object message, bool insertHash);

        void Dequeue<TMessage>(ReceptionAssistant<TMessage> assistant, Action<ReceptionContext<TMessage>> handler);
    }
}
