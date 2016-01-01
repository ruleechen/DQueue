using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace DQueue.Interfaces
{
    public interface IQueueProvider
    {
        void Send<T>(T message);

        T Receive<T>();

        //void Consumer<T>(Func<T> func);
    }
}
