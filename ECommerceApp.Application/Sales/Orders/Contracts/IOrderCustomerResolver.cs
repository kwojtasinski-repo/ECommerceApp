using ECommerceApp.Domain.Sales.Orders;
using System.Threading;
using System.Threading.Tasks;

namespace ECommerceApp.Application.Sales.Orders.Contracts
{
    public interface IOrderCustomerResolver
    {
        Task<OrderCustomer> ResolveAsync(int customerId, CancellationToken ct = default);
    }
}
