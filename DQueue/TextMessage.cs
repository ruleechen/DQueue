using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using DQueue.Interfaces;

namespace DQueue
{
    public class TextMessage : IQueueMessage
    {
        public string QueueName
        {
            get
            {
                return this.GetType().FullName;
            }
        }

        public string Body { get; set; }
    }
}
