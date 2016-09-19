using DQueue.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;

namespace DQueue.BaseHost
{
    public class DQueueHost
    {
        static ILogger Logger = LogFactory.GetLogger();

        private IEnumerable<IQueueService> _queueServices;

        public void Start(string[] args)
        {
            if (_queueServices != null)
            {
                throw new InvalidOperationException("Host already started!");
            }

            Logger.Debug("--------------- Service Host Start -----------------");

            _queueServices = IocContainer.GetQueueServices();

            if (_queueServices != null)
            {
                foreach (var item in _queueServices)
                {
                    item.Start(args);
                }
            }
        }

        public void Stop()
        {
            Logger.Debug("------------------ Service Host Stop --------------");

            if (_queueServices != null)
            {
                foreach (var item in _queueServices)
                {
                    item.Stop();
                }
            }
        }
    }
}
