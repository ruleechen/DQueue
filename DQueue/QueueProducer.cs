using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using DQueue.Interfaces;

namespace DQueue
{
    public class QueueProducer
    {
        public void Send<TMessage>(TMessage message)
            where TMessage : IQueueMessage
        {
            throw new NotImplementedException();
        }
    }
}
