using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using DQueue.Interfaces;

namespace DQueue.WinTest
{
    public partial class Form1 : Form
    {
        public Form1()
        {
            InitializeComponent();
        }

        private class TextMessage : IQueueMessage
        {
            public string QueueName
            {
                get
                {
                    return "TextMessage";
                }
            }

            public string Body
            {
                get;
                set;
            }
        }

        private void btnSend_Click(object sender, EventArgs e)
        {
            QueueManager.Get().Send(new TextMessage
            {
                Body = txtSendMessage.Text
            });
        }

        private void btnReceive_Click(object sender, EventArgs e)
        {
            var message = QueueManager.Get().Receive<TextMessage>();
            txtReceiveMessage.Text = message.Body;
        }
    }
}
