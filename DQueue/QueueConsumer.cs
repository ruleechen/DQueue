using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using DQueue.Interfaces;

namespace DQueue
{
    public class QueueConsumer
    {
        public void Receive<TMessage>(Action<TMessage> action)
        {
            throw new NotImplementedException();
        }
    }
}
