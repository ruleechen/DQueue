using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using DQueue.Interfaces;
using DQueue.QueueProviders;

namespace DQueue
{
    public class QueueHelpers
    {
        public static IQueueProvider GetProvider()
        {
            return new RabbitMQProvider();
        }

        public static string GetQueueName<TMessage>()
            where TMessage : new()
        {
            var obj = new TMessage();

            var imsg = obj as IQueueMessage;

            if (imsg != null)
            {
                return imsg.QueueName;
            }
            else
            {
                return obj.GetType().FullName;
            }
        }
    }
}
