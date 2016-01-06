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
        private readonly QueueProvider _type;
        private readonly int _threads;
        private readonly List<Task> _tasks;

        public QueueConsumer()
            : this(QueueProvider.Configured, 1)
        {
        }

        public QueueConsumer(int threads)
            : this(QueueProvider.Configured, threads)
        {
        }

        public QueueConsumer(QueueProvider type)
            : this(type, 1)
        {
        }

        public QueueConsumer(QueueProvider type, int threads)
        {
            _type = type;
            _threads = threads;
            _tasks = new List<Task>();
        }

        public void Receive<TMessage>(Action<TMessage> handler)
            where TMessage : new()
        {
            Receive<TMessage>((message, context) =>
            {
                handler(message);
                context.Continue();
            });
        }

        public void Receive<TMessage>(Action<TMessage, ReceptionContext> handler)
            where TMessage : new()
        {
            this.Dispose();

            for (var i = 0; i < _threads; i++)
            {
                _tasks.Add(Task.Factory.StartNew(() =>
                {
                    var provider = QueueHelpers.GetProvider(_type);

                    var queueName = QueueHelpers.GetQueueName<TMessage>();

                    provider.Receive<TMessage>(queueName, (message, context) =>
                    {
                        handler(message, context);
                    });

                }));
            }

            //Task.WaitAll(_tasks.ToArray());
        }

        public void Dispose()
        {
            foreach (var task in _tasks)
            {
                task.Dispose();
            }

            _tasks.Clear();
        }
    }
}
