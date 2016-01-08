using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace DQueue.Interfaces
{
    public interface IQueueProvider
    {
        void Enqueue(string queueName, object message);

        void Dequeue<TMessage>(string queueName, Action<TMessage, ReceptionContext> handler);

        ReceptionStatus ReceptionStatus { get; set; }

        void RequestStop();
    }
}
