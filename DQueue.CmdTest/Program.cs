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
            var consumers = new List<QueueConsumer<SampleMessage>>();
            var producer = new QueueProducer();
            producer.IgnoreHash = true;

            var completeCount = 0;
            var timeoutCount = 0;

            for (var i = 0; i < 200; i++)
            {
                var consumer = new QueueConsumer<SampleMessage>("Queue" + i, 10);
                consumers.Add(consumer);

                consumer.Receive((context) =>
                {
                    Thread.Sleep(100);

                    if (!context.Cancellation.IsCancellationRequested)
                    {
                        Console.WriteLine(string.Format("receiver 1, thread {0} -> [{1}]", Task.CurrentId, context.Message.Text));
                    }
                });

                consumer.Receive((context) =>
                {
                    Thread.Sleep(200);

                    if (!context.Cancellation.IsCancellationRequested)
                    {
                        Console.WriteLine(string.Format("receiver 2, thread {0} -> [{1}]", Task.CurrentId, context.Message.Text));
                    }
                });

                consumer.OnComplete((context) =>
                {
                    completeCount++;

                    foreach (var ex in context.Exceptions)
                    {
                        Console.WriteLine("excpetion: [" + ex.Message + "] [" + context.Message.Text + "]");
                    }
                });

                consumer.OnTimeout((context) =>
                {
                    timeoutCount++;
                    Console.WriteLine("timeout: [" + context.Message.Text + "]");
                });

                for (var j = 0; j < 100; j++)
                {
                    var msg = new SampleMessage
                    {
                        Text = "m" + i.ToString() + "-" + j.ToString()
                    };

                    producer.Send("Queue" + i, msg);

                    Console.WriteLine(string.Format("send -> [{0}]", msg.Text));
                }
            }

            while (true)
            {
                var text = Console.ReadLine();

                if (text == "exit")
                {
                    break;
                }
                else if (text == "complete")
                {
                    Console.WriteLine(completeCount);
                    continue;
                }
                else if (text == "timeout")
                {
                    Console.WriteLine(timeoutCount);
                    continue;
                }

                producer.Send("Queue0", new SampleMessage
                {
                    Text = text
                });

                Console.WriteLine(string.Format("send -> [{0}]", text));
            }

            foreach (var consumer in consumers)
            {
                consumer.Dispose();
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
