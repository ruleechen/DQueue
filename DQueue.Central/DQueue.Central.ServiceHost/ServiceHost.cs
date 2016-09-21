using DQueue.Infrastructure;
using System;
using System.Diagnostics;
using System.ServiceProcess;

namespace DQueue.Central.ServiceHost
{
    public partial class ServiceHost : ServiceBase
    {
        const string SERVICENAME = "DQueue.Central.Service";
        static ILogger Logger = LogFactory.GetLogger();

        public ServiceHost()
        {
            InitializeComponent();
            ServiceName = SERVICENAME;
        }

        protected override void OnStart(string[] args)
        {
        }

        protected override void OnStop()
        {
        }
    }
}
