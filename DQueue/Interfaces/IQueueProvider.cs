using System;

namespace DQueue.Interfaces
{
    public interface IQueueProvider
    {
        bool IgnoreHash { get; set; }

        bool ExistsMessage(string queueName, object message);

        void Enqueue(string queueName, object message);

        void Dequeue<TMessage>(ReceptionAssistant assistant, Action<ReceptionContext<TMessage>> handler);
    }
}
