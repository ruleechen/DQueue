using Newtonsoft.Json;

namespace DQueue.Interfaces
{
    public interface IQueueMessage
    {
        [JsonIgnore]
        string QueueName { get; }
    }
}
