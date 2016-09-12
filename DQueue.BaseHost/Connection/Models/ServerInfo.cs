using System;

namespace DQueue.BaseHost.Connection.Models
{
    public class ServerInfo
    {
        public string ServerId { get; set; }
        public string ServerName { get; set; }
        public string ServerAlias { get; set; }
        public string Processor { get; set; }
        public string InstalledMemory { get; set; }
        public string SystemType { get; set; }
        public DateTime StartAt { get; set; }
    }
}
