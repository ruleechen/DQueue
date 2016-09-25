using Newtonsoft.Json;

namespace DQueue.Infrastructure.Connection.Commands
{
    public abstract class BaseCommand : ICommand
    {
        public string Name
        {
            get
            {
                return GetType().Name;
            }
        }

        public string Body
        {
            get
            {
                var body = GetCommandBody();
                return JsonConvert.SerializeObject(body);
            }
        }

        public abstract object GetCommandBody();
    }
}
