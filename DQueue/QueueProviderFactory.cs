using DQueue.Interfaces;
using DQueue.QueueProviders;
using System;

namespace DQueue
{
    public class QueueProviderFactory
    {
        public static IQueueProvider CreateProvider(QueueProvider provider)
        {
            if (provider == QueueProvider.Configured)
            {
                QueueProvider outProvider;
                var strProvider = ConfigSource.Current.AppSettings.Settings["QueueProvider"].Value;
                if (Enum.TryParse<QueueProvider>(strProvider, true, out outProvider))
                {
                    provider = outProvider;
                }
                else
                {
                    throw new ArgumentException("Can not support queue provider: " + strProvider);
                }
            }

            if (provider == QueueProvider.Redis)
            {
                return new RedisProvider();
            }

            if (provider == QueueProvider.RabbitMQ)
            {
                return new RabbitMQProvider();
            }

            if (provider == QueueProvider.AspNet)
            {
                return new AspNetProvider();
            }

            throw new ArgumentException("Can not support queue provider: " + provider.ToString());
        }
    }
}
