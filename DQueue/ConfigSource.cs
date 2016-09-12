using System.Configuration;
using System.Web;
using System.Web.Configuration;

namespace DQueue
{
    public class ConfigSource
    {
        private static Configuration _current;
        public static Configuration Current
        {
            get
            {
                if (_current == null)
                {
                    if (HttpContext.Current.IsAvailable())
                    {
                        _current = WebConfigurationManager.OpenWebConfiguration("~");
                    }
                    else
                    {
                        _current = ConfigurationManager.OpenExeConfiguration(ConfigurationUserLevel.None);
                    }
                }

                return _current;
            }
            set
            {
                _current = value;
            }
        }

        public static string GetAppSettings(string key)
        {
            var item = Current.AppSettings.Settings[key];
            return item != null ? item.Value : null;
        }

        public static string GetConnection(string key)
        {
            var conn = Current.ConnectionStrings.ConnectionStrings[key];
            return conn != null ? conn.ConnectionString : null;
        }
    }
}
