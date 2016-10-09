using System;

namespace DQueue.Interfaces
{
    public interface IQueueConsumer : IDisposable
    {
        string QueueName { get; }

        bool IsAlive();

        void Rescue();
    }
}
