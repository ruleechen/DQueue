using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace DQueue.Interfaces
{
    public interface IQueueProvider
    {
        void Send(string queueName, object message);

        void Receive<TMessage>(string queueName, Action<TMessage, ReceptionContext> handler);
    }
}
