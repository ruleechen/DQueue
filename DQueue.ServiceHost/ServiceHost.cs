using DQueue.Interfaces;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.ServiceProcess;

namespace DQueue.ServiceHost
{
    public partial class ServiceHost : ServiceBase
    {
        const string SERVICENAME = "DQueue.Service";
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
                        Logger.Error("UnhandledException!", (Exception)e.ExceptionObject);
                    }
                    else
                    {
                        Logger.Debug("UnhandledException:" + e.ExceptionObject.ToString());
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
        }

        private IEnumerable<IQueueService> _queueServices;

        protected override void OnStart(string[] args)
        {
            try
            {
                Logger.Debug("--------------- Service Host Start -----------------");

                _queueServices = Injector.GetAllExports<IQueueService>();

                if (_queueServices != null)
                {
                    foreach (var item in _queueServices)
                    {
                        item.Start(args);
                    }
                }
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
