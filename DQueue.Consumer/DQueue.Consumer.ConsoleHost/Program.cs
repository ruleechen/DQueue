using DQueue.Infrastructure;
using System;

namespace DQueue.Consumer.ConsoleHost
{
    class Program
    {
        static ILogger Logger = LogFactory.GetLogger();

        static void Main(string[] args)
        {
            OnStart(args);
            Console.WriteLine("Console Host Started!");

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
            }
            catch (Exception ex)
            {
                Logger.Error("OnStart error!", ex);
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
                }
                catch (Exception ex)
                {
                    Logger.Error("OnStop error!", ex);
                }
            }
        }
    }
}
