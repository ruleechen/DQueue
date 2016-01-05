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

        public void Receive<TMessage>(Action<TMessage> action)
            where TMessage : new()
        {
            Receive<TMessage>(1, (context, message) =>
            {
                action(message);
                context.Complete();
            });
        }

        public void Receive<TMessage>(Action<ConsumerContext, TMessage> action)
            where TMessage : new()
        {
            Receive(1, action);
        }

        public void Receive<TMessage>(int threads, Action<ConsumerContext, TMessage> action)
            where TMessage : new()
        {
            for (var i = 0; i < threads; i++)
            {
                _tasks.Add(Task.Factory.StartNew(() =>
                {
                    var context = new ConsumerContext();
                    var provider = QueueHelpers.GetProvider();
                    action(context, provider.Receive<TMessage>());
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
