using ECommerceApp.Domain.Interface;
using ECommerceApp.Domain.Model;
using ECommerceApp.Infrastructure.Database;
using System.Linq;

namespace ECommerceApp.Infrastructure.Repositories
{
    public class PaymentRepository : GenericRepository<Payment>, IPaymentRepository
    {
        public PaymentRepository(Context context) : base(context)
        {
        }

        public int AddPayment(Payment payment)
        {
            _context.Payments.Add(payment);
            _context.SaveChanges();
            return payment.Id;
        }

        public void DeletePayment(int paymentId)
        {
            var payment = _context.Payments.Find(paymentId);

            if (payment != null)
            {
                _context.Payments.Remove(payment);
                _context.SaveChanges();
            }
        }

        public IQueryable<Payment> GetAllPayments()
        {
            var payments = _context.Payments.AsQueryable();
            return payments;
        }

        public Payment GetPaymentById(int paymentId)
        {
            var payment = _context.Payments.FirstOrDefault(p => p.Id == paymentId);
            return payment;
        }

        public Payment GetPaymentByOrderId(int orderId)
        {
            return _context.Payments.FirstOrDefault(p => p.OrderId == orderId);
        }

        public void UpdatePayment(Payment payment)
        {
            _context.Payments.Update(payment);
            _context.SaveChanges();
        }
    }
}
