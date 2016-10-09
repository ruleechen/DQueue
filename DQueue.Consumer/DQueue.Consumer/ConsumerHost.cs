using DQueue.Interfaces;
using System;
using System.Collections.Generic;

namespace DQueue.Consumer
{
    public class ConsumerHost
    {
        private IEnumerable<IConsumerService> _consumerServices;

        public void Start(string[] args)
        {
            if (_consumerServices != null)
            {
                throw new InvalidOperationException("Host already started!");
            }

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
