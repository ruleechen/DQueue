using System;

namespace DQueue.Infrastructure.Connection.Models
{
    public class HostInfo
    {
        public string HostId { get; set; }
        public string HostAlias { get; set; }
        public string HostPath { get; set; }
        public DateTime StartAt { get; set; }
    }
}
