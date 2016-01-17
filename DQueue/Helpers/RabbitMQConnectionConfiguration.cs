using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace DQueue.Helpers
{
    public class RabbitMQConnectionConfiguration
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
            Port = DefaultPort;
            VirtualHost = "/";
            UserName = "guest";
            Password = "guest";
            RequestedHeartbeat = DefaultHeartBeatInSeconds;
            ClientProperties = new Dictionary<string, object>();
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
}
