namespace DQueue.Infrastructure.Connection
{
    public interface ICommand
    {
        string Name { get; }
        string Body { get; }
    }
}
