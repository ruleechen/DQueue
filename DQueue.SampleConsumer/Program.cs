using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DQueue.SampleConsumer
{
    class Program
    {
        static void Main(string[] args)
        {
            var consumer = new QueueConsumer();

            consumer.Receive<SampleMessage>((message) =>
            {
                Console.WriteLine(message.FirstName);
                Console.WriteLine(message.LastName);
            });
        }
    }
}
