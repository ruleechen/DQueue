using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DQueue.SampleConsumer
{
    class Consumer : BaseConsumer<TextMessage>
    {
        public override void OnReceive(TextMessage message)
        {
            throw new NotImplementedException();
        }
    }

    class Program
    {
        static void Main(string[] args)
        {
            new Consumer().Start();
        }
    }
}
