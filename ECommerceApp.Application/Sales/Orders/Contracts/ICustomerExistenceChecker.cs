using System.Threading;
using System.Threading.Tasks;

namespace ECommerceApp.Application.Sales.Orders.Contracts
{
    public interface ICustomerExistenceChecker
    {
        Task<bool> ExistsAsync(int customerId, CancellationToken ct = default);
    }
}
