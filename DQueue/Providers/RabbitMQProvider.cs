using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using DQueue.Core;
using Newtonsoft.Json;

namespace DQueue.Providers
{
    public class RabbitMQProvider : IQueue
    {
        private static string GetChannel<T>()
        {
            var type = typeof(T);

            if (typeof(IMessage).IsAssignableFrom(type))
            {
                try
                {
                    var instance = (IMessage)Activator.CreateInstance(type);
                    return instance.Channel;
                }
                catch (Exception)
                {
                }
            }

            return type.FullName;
        }

        public void Send<T>(T message)
        {
            var channel = GetChannel<T>();
            var data = JsonConvert.SerializeObject(message);
        }

        public T Receive<T>()
        {
            var channel = GetChannel<T>();
            return default(T);
        }
    }
}
