using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
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
                    if (HttpContext.Current != null)
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
    }
}
