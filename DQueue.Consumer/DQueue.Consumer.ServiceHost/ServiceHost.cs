using System;
using System.Diagnostics;
using System.ServiceProcess;

namespace DQueue.Consumer.ServiceHost
{
    public partial class ServiceHost : ServiceBase
    {
        const string SERVICENAME = "DQueue.Consumer.Service";
        static ILogger Logger = LogFactory.GetLogger();

        static ServiceHost()
        {
            // http://www.devopsonwindows.com/handle-windows-service-errors/
            AppDomain.CurrentDomain.UnhandledException += (s, e) =>
            {
                if (e.ExceptionObject != null)
                {
                    if (e.ExceptionObject is Exception)
                    {
                        Logger.Error("Unhandled Exception!", (Exception)e.ExceptionObject);
                    }
                    else
                    {
                        Logger.Debug("Unhandled Exception:" + e.ExceptionObject.ToString());
                    }
                }

                if (EventLog.SourceExists(SERVICENAME))
                {
                    EventLog.WriteEntry(SERVICENAME,
                        "Fatal Exception : " + Environment.NewLine +
                        e.ExceptionObject, EventLogEntryType.Error);
                }
            };
        }

        public ServiceHost()
        {
            InitializeComponent();
            ServiceName = SERVICENAME;
        }

        private ConsumerHost _consumerHost;

        protected override void OnStart(string[] args)
        {
            try
            {
                _consumerHost = new ConsumerHost();
                _consumerHost.Start(args);
            }
            catch (Exception ex)
            {
                //https://msdn.microsoft.com/en-us/library/windows/desktop/ms681383.aspx
                ExitCode = 1064; // ERROR_EXCEPTION_IN_SERVICE
                Logger.Error("OnStart error!", ex);
                throw;
            }
        }

        protected override void OnStop()
        {
            if (_consumerHost != null)
            {
                try
                {
                    _consumerHost.Stop();
                }
                catch (Exception ex)
                {
                    Logger.Error("OnStop error!", ex);
                }
            }
        }
    }
}
