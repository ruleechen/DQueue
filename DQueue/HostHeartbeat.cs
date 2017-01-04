using System.Timers;

namespace DQueue
{
    public class HostHeartbeat
    {
        static Timer _timer;

        public static void StartTimer()
        {
            StopTimer();

            _timer = new Timer();
            _timer.Interval = Constants.HostHeartbeatInterval.TotalMilliseconds;
            _timer.Elapsed += (s, e) => { Heartbeat(); };
            _timer.Start();
        }

        public static void StopTimer()
        {
            if (_timer != null)
            {
                _timer.Stop();
                _timer.Dispose();
                _timer = null;
            }
        }

        public static void Heartbeat()
        {
            //TODO:
        }
    }
}
