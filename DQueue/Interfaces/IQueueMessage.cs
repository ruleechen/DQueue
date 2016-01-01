using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Newtonsoft.Json;

namespace DQueue.Interfaces
{
    public interface IQueueMessage
    {
        [JsonIgnore]
        string QueueName { get; }
    }
}
