using System;

namespace DQueue
{
    public class Asserter
    {
        static Asserter()
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
            HostHeartbeats.StartTimer();
            ConsumerHealth.StartTimer();
        }

        private static void ProcessExit()
        {
            HostHeartbeats.StopTimer();
            ConsumerHealth.StopTimer();
        }
    }
}
