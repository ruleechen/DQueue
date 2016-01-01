using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace DQueue.Interfaces
{
    public interface IQueueProducer<TMessage>
    {
        void Send(TMessage message);
    }
}
