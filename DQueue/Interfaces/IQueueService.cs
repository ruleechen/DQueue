using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DQueue.Interfaces
{
    public interface IQueueService
    {
        void Start(string[] args);

        void Stop();
    }
}
