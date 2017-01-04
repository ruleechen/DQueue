using DQueue.Infrastructure;
using DQueue.Interfaces;
using System;
using System.Collections.Generic;
using System.Timers;

namespace DQueue
{
    public class ConsumerHealth
    {
        static ILogger Logger = LogFactory.GetLogger("health");
        static HashSet<IQueueConsumer> _consumers;
        static object _locker;
        static Timer _timer;

        static ConsumerHealth()
        {
            _consumers = new HashSet<IQueueConsumer>();
            _locker = new object();
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
                        Logger.Error("Error occurs on diagnosing \"{0}\".", ex);
                    }
                }

                var rescueCount = 0;

                foreach (var consumer in deadConsumers)
                {
                    try
                    {
                        Logger.Info(string.Format("Consumer \"{0}\" diagnosed dead.", consumer.QueueName));

                        consumer.Rescue();

                        rescueCount++;

                        Logger.Info(string.Format("Consumer \"{0}\" is rescued.", consumer.QueueName));
                    }
                    catch (Exception ex)
                    {
                        Logger.Error(string.Format("Error occurs on rescuing \"{0}\".", consumer.QueueName), ex);
                    }
                }

                var status = new HealthStatus
                {
                    DiagnosedDead = deadConsumers.Count,
                    DiagnosedAlive = _consumers.Count - deadConsumers.Count,
                    Rescued = rescueCount,
                    TotalAlive = _consumers.Count - deadConsumers.Count + rescueCount,
                    ConsumerTotal = _consumers.Count,
                    DiagnoseAt = DateTime.Now.ToString("yyyy-MM-dd hh:mm:ss"),
                };

                Logger.Info("Consumer Health Status", status.SerializePretty());
            }
        }

        public class HealthStatus
        {
            public int DiagnosedAlive { get; set; }
            public int DiagnosedDead { get; set; }
            public int Rescued { get; set; }
            public int TotalAlive { get; set; }
            public int ConsumerTotal { get; set; }
            public string DiagnoseAt { get; set; }
        }
    }
}
