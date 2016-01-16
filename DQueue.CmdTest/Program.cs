﻿using System;
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

            consumer.Receive((context) =>
            {
                Thread.Sleep(1000);
                var threadId = Thread.CurrentThread.ManagedThreadId;
                Console.WriteLine(string.Format("[Receiver 1, ThreadID {0}] -> {1}", threadId, context.Message.Text));
            });

            consumer.Receive((context) =>
            {
                Thread.Sleep(1100);
                var threadId = Thread.CurrentThread.ManagedThreadId;
                Console.WriteLine(string.Format("[Receiver 2, ThreadID {0}] -> {1}", threadId, context.Message.Text));
            });

            consumer.Complete((context) =>
            {
                foreach (var ex in context.Exceptions)
                {
                    Console.WriteLine(ex.Message);
                }
            });


            var producer = new QueueProducer();

            while (true)
            {
                var text = Console.ReadLine();

                if (text == "exit")
                {
                    break;
                }

                producer.Send(new SampleMessage
                {
                    Text = text
                });

                Console.WriteLine(string.Format("[send] -> {0}", text));
            }

            consumer.Dispose();
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
