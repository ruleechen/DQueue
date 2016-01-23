using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;

namespace DQueue.Interfaces
{
    public interface IQueueProvider
    {
        void Enqueue(string queueName, object message);

        void Dequeue<TMessage>(ReceptionAssistant assistant, Action<ReceptionContext<TMessage>> handler);
    }
}
