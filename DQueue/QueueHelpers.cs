using System;
using System.Collections.Generic;
using System.Configuration;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using DQueue.Interfaces;
using DQueue.QueueProviders;

namespace DQueue
{
    internal class QueueHelpers
    {
        static Lazy<RabbitMQ.Client.ConnectionFactory> _rabbitMQConnectionFactory = new Lazy<RabbitMQ.Client.ConnectionFactory>(() =>
        {
            var rabbitMQConnectionString = ConfigurationManager.ConnectionStrings["RabbitMQ_Connection"].ConnectionString;
            var rabbitMQConfiguration = RabbitMQConnectionConfiguration.Parse(rabbitMQConnectionString);
            return new RabbitMQ.Client.ConnectionFactory
            {
                HostName = rabbitMQConfiguration.HostName,
                Port = rabbitMQConfiguration.Port,
                VirtualHost = rabbitMQConfiguration.VirtualHost,
                UserName = rabbitMQConfiguration.UserName,
                Password = rabbitMQConfiguration.Password,
                RequestedHeartbeat = rabbitMQConfiguration.RequestedHeartbeat,
                ClientProperties = rabbitMQConfiguration.ClientProperties
            };
        }, true);

        static Lazy<StackExchange.Redis.ConnectionMultiplexer> _redisConnectionFactory = new Lazy<StackExchange.Redis.ConnectionMultiplexer>(() =>
        {
            var redisConnectionString = ConfigurationManager.ConnectionStrings["Redis_Connection"].ConnectionString;
            var resisConfiguration = StackExchange.Redis.ConfigurationOptions.Parse(redisConnectionString);
            return StackExchange.Redis.ConnectionMultiplexer.Connect(resisConfiguration);
        }, true);

        public static IQueueProvider CreateProvider(QueueProvider provider)
        {
            if (provider == QueueProvider.Configured)
            {
                QueueProvider outProvider;
                var strProvider = ConfigurationManager.AppSettings["QueueProvider"];
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
                return new RedisProvider(_redisConnectionFactory.Value);
            }

            if (provider == QueueProvider.RabbitMQ)
            {
                return new RabbitMQProvider(_rabbitMQConnectionFactory.Value);
            }

            if (provider == QueueProvider.AspNet)
            {
                return new AspNetProvider();
            }

            throw new ArgumentException("Can not support queue provider: " + provider.ToString());
        }

        public static string GetQueueName(Type messageType)
        {
            if (typeof(IQueueMessage).IsAssignableFrom(messageType))
            {
                try
                {
                    var instance = (IQueueMessage)Activator.CreateInstance(messageType);
                    return instance.QueueName;
                }
                catch (Exception)
                {
                }
            }

            return messageType.FullName;
        }

        public static string GetQueueName<TMessage>()
            where TMessage : new()
        {
            var obj = new TMessage();

            var imsg = obj as IQueueMessage;

            if (imsg != null)
            {
                return imsg.QueueName;
            }
            else
            {
                return obj.GetType().FullName;
            }
        }

        public static string GetProcessingQueueName(string associatedQueueName)
        {
            return associatedQueueName + "$processing$";
        }
    }

    #region RabbitMQHelpers
    internal class RabbitMQConnectionConfiguration
    {
        // defaults
        public const ushort DefaultPort = 5672;
        public const ushort DefaultHeartBeatInSeconds = 60;

        // fields
        public string HostName { get; set; }
        public ushort Port { get; set; }
        public string VirtualHost { get; set; }
        public string UserName { get; set; }
        public string Password { get; set; }
        public ushort RequestedHeartbeat { get; set; }
        public IDictionary<string, object> ClientProperties { get; set; }

        public RabbitMQConnectionConfiguration()
        {
            this.Port = DefaultPort;
            this.VirtualHost = "/";
            this.UserName = "guest";
            this.Password = "guest";
            this.RequestedHeartbeat = DefaultHeartBeatInSeconds;
            this.ClientProperties = new Dictionary<string, object>();
        }

        public static RabbitMQConnectionConfiguration Parse(string connectionString)
        {
            var config = new RabbitMQConnectionConfiguration();

            var properties = typeof(RabbitMQConnectionConfiguration).GetProperties().Where(x => x.CanWrite);
            foreach (var property in properties)
            {
                var match = Regex.Match(connectionString, string.Format("[^\\w]*{0}=(?<{0}>[^;,]+)", property.Name), RegexOptions.IgnoreCase);
                if (match != null && match.Success)
                {
                    var stringValue = match.Groups[property.Name].Value;
                    var objectValue = TypeDescriptor.GetConverter(property.PropertyType).ConvertFromString(stringValue);
                    property.SetValue(config, objectValue, null);
                }
            }

            return config;
        }
    }
    #endregion
}
