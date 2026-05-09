using ECommerceApp.Application.Messaging;

namespace ECommerceApp.Application.Messaging
{
    public sealed record OrderExistsQuery(int OrderId) : IQuery<bool>;
}
