using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using DQueue.Interfaces;

namespace DQueue
{
    public abstract class BaseConsumer<TMessage> : IQueueConsumer<TMessage>
    {
        public void Start()
        {
        }

        public abstract void OnReceive(TMessage message);
    }
}
