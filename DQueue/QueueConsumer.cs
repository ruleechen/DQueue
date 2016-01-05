using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using DQueue.Interfaces;

namespace DQueue
{
    public class QueueConsumer : IDisposable
    {
        private readonly List<Task> _tasks;

        public QueueConsumer()
        {
            _tasks = new List<Task>();
        }

        public void Receive<TMessage>(Action<TMessage> handler)
            where TMessage : new()
        {
            Receive<TMessage>(1, (message, context) =>
            {
                handler(message);
                context.Complete();
            });
        }

        public void Receive<TMessage>(Action<TMessage, ReceptionContext> handler)
            where TMessage : new()
        {
            Receive<TMessage>(1, handler);
        }

        public void Receive<TMessage>(int threads, Action<TMessage, ReceptionContext> handler)
            where TMessage : new()
        {
            for (var i = 0; i < threads; i++)
            {
                _tasks.Add(Task.Factory.StartNew(() =>
                {
                    var provider = QueueHelpers.GetProvider();

                    var queueName = QueueHelpers.GetQueueName<TMessage>();

                    provider.Receive<TMessage>(queueName, (message, context) =>
                    {
                        handler(message, context);
                    });

                }));
            }

            Task.WaitAll(_tasks.ToArray());
        }

        public void Dispose()
        {
            foreach (var task in _tasks)
            {
                task.Dispose();
            }
        }
    }
}
