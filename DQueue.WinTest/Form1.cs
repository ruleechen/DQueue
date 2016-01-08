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
        private readonly QueueConsumer _consumer;

        public Form1()
        {
            InitializeComponent();

            var control = txtReceive;

            _consumer = new QueueConsumer(10);

            _consumer.Receive<SampleMessage>((message) =>
            {
                Thread.Sleep(5000);
                var threadId = Thread.CurrentThread.ManagedThreadId;

                control.Text += string.Format("[ThreadID {0}, Received] -> {1} {2}", threadId, message.FirstName, message.LastName) + Environment.NewLine;
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
                FirstName = text,
                LastName = text
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

        public string FirstName { get; set; }

        public string LastName { get; set; }
    }
}
