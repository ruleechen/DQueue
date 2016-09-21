using Microsoft.Owin;
using Owin;

[assembly: OwinStartupAttribute(typeof(DQueue.Central.WebSite.Startup))]
namespace DQueue.Central.WebSite
{
    public partial class Startup
    {
        public void Configuration(IAppBuilder app)
        {
            ConfigureAuth(app);
        }
    }
}
