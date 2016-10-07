using DQueue.Infrastructure;
using System;

namespace DQueue.Consumer.ConsoleHost
{
    class Program
    {
        static ILogger Logger = LogFactory.GetLogger("console-host");

        static void Main(string[] args)
        {
            OnStart(args);
            Console.WriteLine("Host Started!");

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
                        Logger.Error("Unhandled Exception!", e.ExceptionObject.ToString());
                    }
                }
            };

            AppDomain.CurrentDomain.ProcessExit += (s, e) =>
            {
                OnStop();
            };

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

        static ConsumerHost _consumerHost;

        static void OnStart(string[] args)
        {
            try
            {
                _consumerHost = new ConsumerHost();
                _consumerHost.Start(args);
                Logger.Info("Host Started!");
            }
            catch (Exception ex)
            {
                Logger.Error("OnStart Error!", ex);
            }
        }

        static void OnStop()
        {
            if (_consumerHost != null)
            {
                try
                {
                    _consumerHost.Stop();
                    _consumerHost = null;
                    Logger.Info("Host Stopped!");
                }
                catch (Exception ex)
                {
                    Logger.Error("OnStop Error!", ex);
                }
            }
        }
    }
}
