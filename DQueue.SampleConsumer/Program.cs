using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DQueue.SampleConsumer
{
    class Consumer : BaseConsumer<TextMessage>
    {
    }

    class Program
    {
        static void Main(string[] args)
        {
            var consumer = new Consumer();

            while (true)
            {
                if (consumer.Receive())
                {

                }
            }
        }
    }
}
