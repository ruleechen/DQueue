using DQueue.BaseHost;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DQueue.ConsoleHost
{
    class Program
    {
        static ILogger Logger = LogFactory.GetLogger();

        static void Main(string[] args)
        {
            OnStart(args);
            Console.WriteLine("Console Host Started!");

            while (true)
            {
                var input = Console.ReadLine();
                if (input == "exit")
                {
                    OnStop();
                    break;
                }
            }
        }

        static DQueueHost _dqueueHost;

        static void OnStart(string[] args)
        {
            try
            {
                _dqueueHost = new DQueueHost();
                _dqueueHost.Start(args);
            }
            catch (Exception ex)
            {
                Logger.Error("OnStart error!", ex);
            }
        }

        static void OnStop()
        {
            if (_dqueueHost != null)
            {
                try
                {
                    _dqueueHost.Stop();
                }
                catch (Exception ex)
                {
                    Logger.Error("OnStop error!", ex);
                }
            }
        }
    }
}
