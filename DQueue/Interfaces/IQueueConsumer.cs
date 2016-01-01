using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace DQueue.Interfaces
{
    public interface IQueueConsumer<TMessage>
    {
        void OnReceive(TMessage message);
    }
}
