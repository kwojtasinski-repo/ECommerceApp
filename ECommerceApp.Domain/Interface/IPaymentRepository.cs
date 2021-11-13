using ECommerceApp.Domain.Model;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace ECommerceApp.Domain.Interface
{
    public interface IPaymentRepository : IGenericRepository<Payment>
    {
        void DeletePayment(int paymentId);
        int AddPayment(Payment payment);
        Payment GetPaymentById(int paymentId);
        IQueryable<Payment> GetAllPayments();
        void UpdatePayment(Payment payment);
    }
}
