using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DQueue.SampleProducer
{
    class Producer : BaseProducer<TextMessage>
    {
    }

    class Program
    {
        static void Main(string[] args)
        {
            var producer = new Producer();

            while (true)
            {
                var text = Console.ReadLine();
                producer.Send(new TextMessage { Body = text });
                Console.WriteLine("[send success]");
            }
        }
    }
}
