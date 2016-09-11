using DQueue.Interfaces;
using System;
using System.Collections.Generic;
using System.ComponentModel.Composition.Hosting;
using System.Configuration;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.ServiceProcess;

namespace DQueue.ServiceHost
{
    public partial class ServiceHost : ServiceBase
    {
        static CompositionContainer _container;

        static ServiceHost()
        {
            // Cause if you load the assembly by using Assembly.LoadFile() the assembly will automatically be put into your CurrentDomain
            var assembiles = Directory.GetFiles(AppDomain.CurrentDomain.BaseDirectory, "*.dll").Select(x => Assembly.LoadFile(x));

            var catalog = new AggregateCatalog();
            foreach (var assembily in assembiles)
            {
                var item = new AssemblyCatalog(assembily);
                catalog.Catalogs.Add(item);
            }

            _container = new CompositionContainer(catalog);
        }

        private const string SERVICENAME = "DQueue.Service";

        public ServiceHost()
        {
            InitializeComponent();

            // http://www.devopsonwindows.com/handle-windows-service-errors/
            AppDomain.CurrentDomain.UnhandledException += (s, e) =>
            {
                if (e.ExceptionObject != null)
                {
                    LogError(e.ExceptionObject.ToString());
                }

                if (EventLog.SourceExists(SERVICENAME))
                {
                    EventLog.WriteEntry(SERVICENAME,
                        "Fatal Exception : " + Environment.NewLine +
                        e.ExceptionObject, EventLogEntryType.Error);
                }
            };
        }

        private static void LogError(string message)
        {
            var file = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "error.log");
            var content = string.Format("{0} {1}", DateTime.Now.ToString("yyyy-MM-dd hh:mm:ss"), message);
            try { File.AppendAllText(file, content + Environment.NewLine); } catch { }
        }

        private IEnumerable<IQueueService> _queueServices;

        protected override void OnStart(string[] args)
        {
            //System.Diagnostics.Debugger.Launch();

            try
            {
                LogError("---------------启动对列-----------------");

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
                LogError(ex.ToString());
                throw;
            }
        }

        protected override void OnStop()
        {
            if (_queueServices != null)
            {
                LogError("------------------暂停对列--------------");

                foreach (var item in _queueServices)
                {
                    item.Stop();
                }
            }
        }

    }
}
