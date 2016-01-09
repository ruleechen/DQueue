using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using DQueue.Interfaces;

namespace DQueue.CmdTest
{
    class Program
    {
        static void Main(string[] args)
        {
            var consumer = new QueueConsumer<SampleMessage>(10);

            consumer.Receive((message) =>
            {
                Thread.Sleep(5000);
                var threadId = Thread.CurrentThread.ManagedThreadId;
                Console.WriteLine(string.Format("[ThreadID {0}, Received] -> {1}", threadId, message.Text));
            });


            var producer = new QueueProducer();

            while (true)
            {
                var text = Console.ReadLine();

                producer.Send(new SampleMessage
                {
                    Text = text
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

        public string Text { get; set; }
    }
}
