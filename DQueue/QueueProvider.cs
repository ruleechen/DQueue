using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace DQueue
{
    public enum QueueProvider
    {
        Configured,

        AspNet,

        Redis,

        RabbitMQ
    }
}
