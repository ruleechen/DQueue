using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using DQueue.Interfaces;

namespace DQueue.WinTest
{
    public partial class Form1 : Form
    {
        private readonly QueueConsumer<SampleMessage> _consumer;

        public Form1()
        {
            InitializeComponent();

            _consumer = new QueueConsumer<SampleMessage>(50);

            _consumer.Receive((context) =>
            {
                Thread.Sleep(3000);

                if (context.DispatchStatus != DispatchStatus.None)
                {
                    WriteLog(string.Format("[Receiver 1, ThreadID {0}] -> {1}", Task.CurrentId, context.Message.Text));
                }
            });

            _consumer.Receive((context) =>
            {
                Thread.Sleep(4000);

                if (context.DispatchStatus != DispatchStatus.None)
                {
                    WriteLog(string.Format("[Receiver 2, ThreadID {0}] -> {1}", Task.CurrentId, context.Message.Text));
                }
            });


            var start = DateTime.Now;

            _consumer.DispatchStatusChange += (sender, ev) =>
            {
                if (ev.Context.DispatchStatus == DispatchStatus.None)
                {
                    start = DateTime.Now;
                }
            };

            _consumer.OnTimeout((context) =>
            {
                WriteLog(string.Format("Timeout Elapsed: {0}", DateTime.Now - start));
            });
        }

        private void WriteLog(string messagae)
        {
            txtReceive.Text += messagae + Environment.NewLine;
            txtReceive.SelectionStart = txtReceive.Text.Length;
            txtReceive.ScrollToCaret();
        }

        private void btnSend_Click(object sender, EventArgs e)
        {
            var producer = new QueueProducer();
            producer.IgnoreHash = true;

            var text = txtSend.Text;

            producer.Send(new SampleMessage
            {
                Text = text
            });

            txtSend.Focus();
        }

        private void Form1_FormClosing(object sender, FormClosingEventArgs e)
        {
            _consumer.Dispose();
        }
    }

    public class SampleMessage : IQueueMessage
    {
        public string QueueName
        {
            get
            {
                return "TestQueue";
            }
        }

        public string Text { get; set; }
    }
}
