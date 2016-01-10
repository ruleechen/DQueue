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

            var control = txtReceive;

            _consumer = new QueueConsumer<SampleMessage>(10);

            _consumer.Receive((message) =>
            {
                Thread.Sleep(1000);
                var threadId = Thread.CurrentThread.ManagedThreadId;

                control.Text += string.Format("[Receiver 1, ThreadID {0}] -> {1}", threadId, message.Text) + Environment.NewLine;
                control.SelectionStart = control.Text.Length;
                control.ScrollToCaret();
            });

            _consumer.Receive((message) =>
            {
                Thread.Sleep(1100);
                var threadId = Thread.CurrentThread.ManagedThreadId;

                control.Text += string.Format("[Receiver 2, ThreadID {0}] -> {1}", threadId, message.Text) + Environment.NewLine;
                control.SelectionStart = control.Text.Length;
                control.ScrollToCaret();
            });
        }

        private void btnSend_Click(object sender, EventArgs e)
        {
            var producer = new QueueProducer();

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
