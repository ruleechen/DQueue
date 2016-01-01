using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DQueue.SampleProducer
{
    class Program
    {
        static void Main(string[] args)
        {
            var producer = new QueueProducer();

            while (true)
            {
                var text = Console.ReadLine();

                producer.Send(new SampleMessage
                {
                    FirstName = text,
                    LastName = text
                });

                Console.WriteLine("[send success]");
            }
        }
    }
}
