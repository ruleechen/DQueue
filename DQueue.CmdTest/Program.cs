using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using DQueue.Interfaces;

namespace DQueue.CmdTest
{
    class Program
    {
        static void Main(string[] args)
        {
            var consumer = new QueueConsumer(10);

            consumer.Receive<SampleMessage>((message) =>
            {
                System.Threading.Thread.Sleep(5000);
                Console.WriteLine(string.Format("[receive] -> {0} {1}", message.FirstName, message.LastName));
            });


            var producer = new QueueProducer();

            while (true)
            {
                var text = Console.ReadLine();

                producer.Send(new SampleMessage
                {
                    FirstName = text,
                    LastName = text
                });

                Console.WriteLine(string.Format("[send] -> {0}", text));
            }
        }
    }

    public class SampleMessage : IQueueMessage
    {
        public string QueueName
        {
            get
            {
                return "TestQueue";
            }
        }

        public string FirstName { get; set; }

        public string LastName { get; set; }
    }
}
