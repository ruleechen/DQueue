namespace DQueue.Consumer.Connection
{
    public interface ICommand
    {
        string Name { get; }
        string Body { get; }
    }
}
