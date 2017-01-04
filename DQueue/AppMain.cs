using System;

namespace DQueue
{
    public class AppMain
    {
        static AppMain()
        {
            ProcessStart();
            AppDomain.CurrentDomain.ProcessExit += (s, e) => { ProcessExit(); };
        }

        public static void Trigger()
        {
            // empty implementaion
            // this is for trigger the static constructor
        }

        private static void ProcessStart()
        {
            ConsumerHealth.StartTimer();
        }

        private static void ProcessExit()
        {
            ConsumerHealth.StopTimer();
        }
    }
}
