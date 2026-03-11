using System.Threading;
using System.Threading.Tasks;

namespace ECommerceApp.Domain.Sales.Payments
{
    public interface IPaymentRepository
    {
        Task<Payment?> GetByIdAsync(int id, CancellationToken ct = default);
        Task<Payment?> GetByOrderIdAsync(int orderId, CancellationToken ct = default);
        Task AddAsync(Payment payment, CancellationToken ct = default);
        Task UpdateAsync(Payment payment, CancellationToken ct = default);
    }
}
