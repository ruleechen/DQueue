using DQueue.Infrastructure;
using System;
using System.Diagnostics;
using System.ServiceProcess;

namespace DQueue.Central.ServiceHost
{
    public partial class ServiceHost : ServiceBase
    {
        static ILogger Logger = LogFactory.GetLogger();

        public ServiceHost()
        {
            InitializeComponent();
            ServiceName = Constants.SERVICE_NAME;
        }

        protected override void OnStart(string[] args)
        {
        }

        protected override void OnStop()
        {
        }
    }
}
