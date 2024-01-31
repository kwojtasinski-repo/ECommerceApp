using ECommerceApp.Domain.Model;
using System.Collections.Generic;

namespace ECommerceApp.Domain.Interface
{
    public interface IPaymentRepository
    {
        bool DeletePayment(int paymentId);
        int AddPayment(Payment payment);
        Payment GetPaymentById(int paymentId);
        List<Payment> GetAllPayments();
        void UpdatePayment(Payment payment);
        Payment GetPaymentByOrderId(int orderId);
        bool DeletePayment(Payment payment);
        Payment GetPaymentDetailsByIdAndUserId(int paymentId, string userId);
        List<Payment> GetAllPayments(int pageSize, int pageNo, string searchString);
        List<Payment> GetAllUserPayments(string userId);
        int GetCountBySearchString(string searchString);
        bool ExistsByIdAndUserId(int id, string userId);
    }
}
