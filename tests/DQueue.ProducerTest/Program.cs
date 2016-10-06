using DQueue.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DQueue.ProducerTest
{
    class Program
    {
        static void Main(string[] args)
        {
            var producer = new QueueProducer();
            producer.IgnoreHash = true;

            while (true)
            {
                var text = Console.ReadLine();

                if (text == "exit")
                {
                    break;
                }

                var parts = text.Split('|');

                if (parts.Length == 2)
                {
                    producer.Send(parts[0], new SampleMessage
                    {
                        Text = parts[1]
                    });

                    Console.WriteLine("sent");
                }
                else
                {
                    Console.WriteLine("invalid");
                }
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

        public string Text { get; set; }
    }
}
