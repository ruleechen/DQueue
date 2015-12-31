using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using DQueue.Core;
using DQueue.Providers;

namespace DQueue
{
    public class QueueManager
    {
        public static IQueue Get()
        {
            return new RabbitMQProvider();
        }
    }
}
