namespace DQueue.BaseHost.Connection
{
    public interface ICommand
    {
        string Name { get; }
        string Body { get; }
    }
}
