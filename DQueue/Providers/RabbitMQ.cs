using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using DQueue.Core;

namespace DQueue.Providers
{
    public class RabbitMQ : IQueue
    {
        public void Send(IMessage message)
        {
            throw new NotImplementedException();
        }

        public IMessage Receive()
        {
            throw new NotImplementedException();
        }
    }
}
