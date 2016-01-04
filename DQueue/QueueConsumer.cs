using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using DQueue.Interfaces;

namespace DQueue
{
    public class QueueConsumer : IDispose
    {
        public void Receive<TMessage>(Action<TMessage> action)
            where TMessage : IQueueMessage
        {
            throw new NotImplementedException();
        }
        
        public void Receive<TMessage>(int threads, Action<ConsumerContext, TMessage> action)
        {
            Task serverTask = Task.Factory.StartNew(() => Server(context));
            Task clientTask = Task.Factory.StartNew(() => Client(context));
            Task.WaitAll(serverTask, clientTask);
            
            Task.StartNew(x =>
            {
                
                var ctx = new ConsumerContext();
                ctx.OnComplete += () => 
                {
                };
                
                action(ctx, message)
            })
        }
    }
}
