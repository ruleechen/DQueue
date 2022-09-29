using DQueue.Helpers;
using DQueue.Interfaces;
using DQueue.QueueProviders;
using System;
using System.Collections.Generic;

namespace DQueue
{
    public class QueueProviderFactory
    {
        static Dictionary<QueueProvider, IQueueProvider> _singletons;

        static QueueProviderFactory()
        {
            _singletons = new Dictionary<QueueProvider, IQueueProvider>();
        }

        public static IQueueProvider GetSingleton(QueueProvider provider)
        {
            if (!_singletons.ContainsKey(provider))
            {
                lock (typeof(QueueProviderFactory))
                {
                    if (!_singletons.ContainsKey(provider))
                    {
                        _singletons[provider] = CreateProvider(provider);
                    }
                }
            }

            return _singletons[provider];
        }

        public static IQueueProvider CreateProvider(QueueProvider provider, bool singleton = false)
        {
            if (provider == QueueProvider.Configured)
            {
                provider = DQueueSettings.Get().Provider;
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
