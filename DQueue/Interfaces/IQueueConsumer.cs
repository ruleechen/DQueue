using System;

namespace DQueue.Interfaces
{
    public interface IQueueConsumer : IDisposable
    {
        bool IsAlive();

        void Rescue();
    }
}
