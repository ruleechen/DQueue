using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace DQueue.Interfaces
{
    public class CancellationPack
    {
        private object _locker;
        private CancellationToken _token;
        private Dictionary<int, List<Action>> _levels;

        public CancellationPack(CancellationToken token)
        {
            _token = token;
            _locker = new object();
            _levels = new Dictionary<int, List<Action>>();

            token.Register(() =>
            {
                var list = _levels.ToList()
                    .OrderBy(x => x.Key);

                foreach (var level in list)
                {
                    foreach (var action in level.Value)
                    {
                        action();
                    }

                    Thread.Sleep(100);
                }
            });
        }

        public void Register(int level, bool exclusive, Action action)
        {
            if (!_levels.ContainsKey(level))
            {
                lock (_locker)
                {
                    if (!_levels.ContainsKey(level))
                    {
                        _levels.Add(level, new List<Action>());
                    }
                }
            }

            var actions = _levels[level];

            lock (_locker)
            {
                if (exclusive)
                {
                    actions.Clear();
                }

                actions.Add(action);
            }
        }
    }
}
