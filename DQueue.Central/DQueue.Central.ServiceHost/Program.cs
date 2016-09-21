using System.ServiceProcess;

namespace DQueue.Central.ServiceHost
{
    static class Program
    {
        /// <summary>
        /// The main entry point for the application.
        /// </summary>
        static void Main()
        {
            ServiceBase[] ServicesToRun;
            ServicesToRun = new ServiceBase[] 
            { 
                new ServiceHost() 
            };
            ServiceBase.Run(ServicesToRun);
        }
    }
}
