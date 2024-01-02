using ECommerceApp.Application.ViewModels.Order;
using ECommerceApp.Domain.Interface;
using ECommerceApp.Domain.Model;
using ECommerceApp.Infrastructure.Database;
using Microsoft.EntityFrameworkCore;
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
            return _context.Payments
                 .Include(p => p.Currency)
                 .Include(p => p.Customer)
                 .Include(p => p.Order)
                 .Where(p => p.Id == paymentId)
                 .Select(p => new Payment
                 {
                     Id = paymentId,
                     DateOfOrderPayment = p.DateOfOrderPayment,
                     Number = p.Number,
                     State = p.State,
                     Cost = p.Cost,
                     CurrencyId = p.CurrencyId,
                     CustomerId = p.CustomerId,
                     OrderId = p.OrderId,
                     Currency = new Currency
                     {
                         Id = p.CurrencyId,
                         Code = p.Currency.Code
                     },
                     Customer = new Customer
                     {
                         Id = p.CustomerId,
                         FirstName = p.Customer.FirstName,
                         LastName = p.Customer.LastName,
                         IsCompany = p.Customer.IsCompany,
                         CompanyName = p.Customer.CompanyName,
                         UserId = p.Customer.UserId,
                         NIP = p.Customer.NIP
                     },
                     Order = new Order
                     {
                         Id = p.OrderId,
                         Number = _context.Orders.Where(o => o.Id == p.OrderId)
                            .Select(o => o.Number).FirstOrDefault(),
                     }
                 })
                 .FirstOrDefault();
        }

        public Payment GetPaymentByOrderId(int orderId)
        {
            return _context.Payments
                .Include(p => p.Currency)
                .Include(p => p.Customer)
                .Include(p => p.Order)
                .Where(p => p.OrderId == orderId)
                .Select(p => new Payment
                {
                    Id = p.Id,
                    DateOfOrderPayment = p.DateOfOrderPayment,
                    Number = p.Number,
                    State = p.State,
                    Cost = p.Cost,
                    CurrencyId = p.CurrencyId,
                    CustomerId = p.CustomerId,
                    OrderId = p.OrderId,
                    Currency = new Currency
                    {
                        Id = p.CurrencyId,
                        Code = p.Currency.Code
                    },
                    Customer = new Customer
                    {
                        Id = p.CustomerId,
                        FirstName = p.Customer.FirstName,
                        LastName = p.Customer.LastName,
                        IsCompany = p.Customer.IsCompany,
                        UserId = p.Customer.UserId,
                        NIP = p.Customer.NIP,
                        CompanyName = p.Customer.CompanyName
                    },
                    Order = new Order
                    {
                        Id = p.OrderId,
                        Number = _context.Orders.Where(o => o.Id == orderId)
                            .Select(o => o.Number).FirstOrDefault(),
                    }
                })
                .FirstOrDefault();
        }

        public void UpdatePayment(Payment payment)
        {
            _context.Payments.Update(payment);
            _context.SaveChanges();
        }
    }
}
