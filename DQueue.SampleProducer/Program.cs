using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DQueue.SampleProducer
{
    class Producer : BaseProducer<TextMessage>
    {
    }

    class Program
    {
        static void Main(string[] args)
        {
            new Producer().Send(new TextMessage
            {
                Body = "hello, dqueue"
            });
        }
    }
}
