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
        static ILogger Logger = LogFactory.GetLogger();
        static HashSet<IQueueConsumer> _consumers;
        static object _locker;
        static Timer _timer;

        static ConsumerHealth()
        {
            _consumers = new HashSet<IQueueConsumer>();
            _locker = new object();
            StartTimer();
        }

        public static void Register(IQueueConsumer consumer)
        {
            lock (_locker)
            {
                if (!_consumers.Contains(consumer))
                {
                    _consumers.Add(consumer);
                }
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
            lock (_locker)
            {
                var deadConsumers = new List<IQueueConsumer>();

                foreach (var consumer in _consumers)
                {
                    try
                    {
                        if (!consumer.IsAlive())
                        {
                            deadConsumers.Add(consumer);
                        }
                    }
                    catch (Exception ex)
                    {
                        Logger.Error("[ConsumerHealth] Error occurs on diagnosing \"{0}\".", ex);
                    }
                }

                foreach (var consumer in deadConsumers)
                {
                    try
                    {
                        Logger.Debug(string.Format("[ConsumerHealth] Consumer \"{0}\" diagnosed dead.", consumer.QueueName));

                        consumer.Rescue();

                        Logger.Debug(string.Format("[ConsumerHealth] Consumer \"{0}\" is rescued.", consumer.QueueName));
                    }
                    catch (Exception ex)
                    {
                        Logger.Error(string.Format("[ConsumerHealth] Error occurs on rescuing \"{0}\".", consumer.QueueName), ex);
                    }
                }
            }
        }
    }
}
