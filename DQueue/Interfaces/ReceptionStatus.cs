namespace DQueue.Interfaces
{
    public enum ReceptionStatus
    {
        Listen,

        Process,

        Complete,

        Retry,

        //Suspend,

        Withdraw
    }
}
