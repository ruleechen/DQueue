using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using DQueue.Interfaces;

namespace DQueue
{
    public abstract class BaseProducer<TMessage> : IQueueProducer<TMessage>
    {
        public void Send(TMessage message)
        {

        }
    }
}
