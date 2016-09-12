using System.Web;

namespace DQueue
{
    public static class HttpContextExtensions
    {
        public static bool IsAvailable(this HttpContext context)
        {
            return context != null && context.Handler != null;
        }
    }
}
