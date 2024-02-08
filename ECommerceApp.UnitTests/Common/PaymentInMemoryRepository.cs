using ECommerceApp.Domain.Interface;
using ECommerceApp.Domain.Model;
using System.Collections.Generic;
using System.Linq;

namespace ECommerceApp.UnitTests.Common
{
    internal sealed class PaymentInMemoryRepository : IPaymentRepository
    {
        private readonly GenericInMemoryRepository<Payment> _repo = new();

        public int AddPayment(Payment payment)
        {
            return _repo.Add(payment);
        }

        public bool DeletePayment(int paymentId)
        {
            return _repo.Delete(paymentId);
        }

        public bool DeletePayment(Payment payment)
        {
            return _repo.Delete(payment);
        }

        public bool ExistsBydId(int id)
        {
            return _repo.GetById(id) is not null;
        }

        public bool ExistsByIdAndUserId(int id, string userId)
        {
            var payment = _repo.GetById(id);
            return payment is not null && payment.Id == id && payment.Customer.UserId == userId;
        }

        public List<Payment> GetAllPayments()
        {
            return _repo.GetAll().ToList();
        }

        public List<Payment> GetAllPayments(int pageSize, int pageNo, string searchString)
        {
            return _repo.GetAll().Where(p => p.Number.StartsWith(searchString)).Skip(pageSize * (pageNo - 1)).Take(pageSize).ToList();
        }

        public List<Payment> GetAllUserPayments(string userId)
        {
            return _repo.GetAll().Where(p => p.Customer.UserId == userId).ToList();
        }

        public int GetCountBySearchString(string searchString)
        {
            return _repo.GetAll().Where(p => p.Number.StartsWith(searchString)).Count();
        }

        public Payment GetPaymentById(int paymentId)
        {
            return _repo.GetById(paymentId);
        }

        public Payment GetPaymentByOrderId(int orderId)
        {
            return _repo.GetAll().Where(p => p.OrderId == orderId).FirstOrDefault();
        }

        public Payment GetPaymentDetailsByIdAndUserId(int paymentId, string userId)
        {
            return _repo.GetAll().Where(p => p.Id == paymentId && p.Customer.UserId == userId).FirstOrDefault();
        }

        public void UpdatePayment(Payment payment)
        {
            _repo.Update(payment);
        }
    }
}
