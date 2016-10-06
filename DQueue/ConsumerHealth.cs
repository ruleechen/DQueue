using DQueue.Infrastructure;
using DQueue.Interfaces;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Timers;

namespace DQueue
{
    public class ConsumerHealth
    {
        static Timer _timer;
        static IDictionary<IQueueConsumer, string> _consumers;

        static ConsumerHealth()
        {
            _consumers = new ConcurrentDictionary<IQueueConsumer, string>();

            StartTimer();
        }

        public static void Register(IQueueConsumer consumer)
        {
            if (!_consumers.ContainsKey(consumer))
            {
                _consumers.Add(consumer, string.Empty);
            }
        }

        public static void StartTimer()
        {
            StopTimer();

            _timer = new Timer();
            _timer.Interval = Constants.ConsumerHealthInterval.TotalMilliseconds;
            _timer.Elapsed += (s, e) => { Diagnose(); };
            _timer.Start();
        }

        public static void StopTimer()
        {
            if (_timer != null)
            {
                _timer.Stop();
                _timer.Dispose();
                _timer = null;
            }
        }

        public static void Diagnose()
        {
            try
            {
                var dead = _consumers.Where(x => !x.Key.IsAlive());

                foreach (var item in dead)
                {
                    item.Key.Rescue();
                }
            }
            catch (Exception ex)
            {
                LogFactory.GetLogger().Error("[ConsumerHealth] Error occurs on Diagnose", ex);
            }
        }
    }
}
