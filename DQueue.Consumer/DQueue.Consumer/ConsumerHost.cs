using DQueue.Infrastructure;
using DQueue.Interfaces;
using System;
using System.Collections.Generic;

namespace DQueue.Consumer
{
    public class ConsumerHost
    {
        static ILogger Logger = LogFactory.GetLogger();

        private IEnumerable<IConsumerService> _consumerServices;

        public void Start(string[] args)
        {
            if (_consumerServices != null)
            {
                throw new InvalidOperationException("Host already started!");
            }

            Logger.Debug("--------------- Consumer Service Host Start -----------------");

            _consumerServices = IocContainer.GetConsumerServices();

            if (_consumerServices != null)
            {
                foreach (var item in _consumerServices)
                {
                    item.Start(args);
                }
            }
        }

        public void Stop()
        {
            Logger.Debug("------------------ Consumer Service Host Stop --------------");

            if (_consumerServices != null)
            {
                foreach (var item in _consumerServices)
                {
                    item.Stop();
                }
            }
        }
    }
}
