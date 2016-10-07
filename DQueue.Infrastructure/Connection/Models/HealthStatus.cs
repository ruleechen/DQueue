namespace DQueue.Infrastructure.Connection.Models
{
    public class HealthStatus
    {
        public int DiagnosedAlive { get; set; }
        public int DiagnosedDead { get; set; }
        public int Rescued { get; set; }
        public int TotalAlive { get; set; }
        public int ConsumerTotal { get; set; }
        public string DiagnoseAt { get; set; }
    }
}
