using Microsoft.AspNetCore.Hosting;

[assembly: HostingStartup(typeof(ECommerceApp.Web.Areas.Identity.IdentityHostingStartup))]
namespace ECommerceApp.Web.Areas.Identity
{
    public class IdentityHostingStartup : IHostingStartup
    {
        public void Configure(IWebHostBuilder builder)
        {
            builder.ConfigureServices((context, services) => {
            });
        }
    }
}