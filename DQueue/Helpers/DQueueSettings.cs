using DQueue.Infrastructure;
using System;

namespace DQueue.Helpers
{
    public class DQueueSettings
    {
        private static DQueueSettings _setts;

        public static DQueueSettings Get()
        {
            if (_setts == null)
            {
                _setts = GetCore();
            }

            return _setts;
        }

        private static DQueueSettings GetCore()
        {
            var hostId = ConfigSource.FirstAppSetting("DQueue.HostId", "HostId");
            var provider = ConfigSource.FirstAppSetting("DQueue.Provider", "QueueProvider");
            var consumerTimeout = ConfigSource.FirstAppSetting("DQueue.ConsumerTimeout", "ConsumerTimeout");

            var settings = new DQueueSettings
            {
                HostId = hostId,
                Provider = provider.AsQueueProvider(),
                ConsumerTimeout = consumerTimeout.AsNullableTimeSpan()
            };

            return settings;
        }

        public string HostId { get; set; }
        public QueueProvider Provider { get; set; }
        public TimeSpan? ConsumerTimeout { get; set; }
    }
}
