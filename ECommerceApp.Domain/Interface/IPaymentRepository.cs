using ECommerceApp.Domain.Model;
using System.Linq;

namespace ECommerceApp.Domain.Interface
{
    public interface IPaymentRepository
    {
        bool DeletePayment(int paymentId);
        int AddPayment(Payment payment);
        Payment GetPaymentById(int paymentId);
        IQueryable<Payment> GetAllPayments();
        void UpdatePayment(Payment payment);
        Payment GetPaymentByOrderId(int orderId);
        bool DeletePayment(Payment payment);
    }
}
