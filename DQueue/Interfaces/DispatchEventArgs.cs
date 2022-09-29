using System;

namespace DQueue.Interfaces
{
    public class DispatchEventArgs<TMessage> : EventArgs
    {
        public DispatchContext<TMessage> Context { get; private set; }

        public DispatchEventArgs(DispatchContext<TMessage> context)
        {
            Context = context;
        }
    }
}
