using ECommerceApp.Infrastructure.Supporting.Communication.Hubs;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;

namespace ECommerceApp.Infrastructure.Supporting.Communication
{
    public static class HubEndpointExtensions
    {
        public static IEndpointRouteBuilder MapCommunicationHubs(this IEndpointRouteBuilder endpoints)
        {
            endpoints.MapHub<NotificationHub>("/hubs/notifications");
            return endpoints;
        }
    }
}
