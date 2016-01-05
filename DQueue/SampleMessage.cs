using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using DQueue.Interfaces;

namespace DQueue
{
    public class SampleMessage : IQueueMessage
    {
        public string QueueName
        {
            get
            {
                return "TestQueue";
            }
        }

        public string FirstName { get; set; }

        public string LastName { get; set; }
    }
}
