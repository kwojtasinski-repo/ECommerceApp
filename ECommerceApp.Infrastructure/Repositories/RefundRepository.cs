using ECommerceApp.Domain.Interface;
using ECommerceApp.Domain.Model;
using ECommerceApp.Infrastructure.Database;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace ECommerceApp.Infrastructure.Repositories
{
    public class RefundRepository : GenericRepository<Refund>, IRefundRepository
    {
        public RefundRepository(Context context) : base(context)
        {
        }

        public int AddRefund(Refund refund)
        {
            _context.Refunds.Add(refund);
            _context.SaveChanges();
            return refund.Id;
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

        public void DeleteRefund(int refundId)
        {
            var refund = _context.Refunds.Find(refundId);

            if (refund != null)
            {
                _context.Refunds.Remove(refund);
                _context.SaveChanges();
            }
        }

        public IQueryable<Refund> GetAllRefunds()
        {
            var refunds = _context.Refunds.AsQueryable();
            return refunds;
        }

        public Refund GetRefundById(int refundId)
        {
            var refund = _context.Refunds.Where(p => p.Id == refundId).FirstOrDefault();
            return refund;
        }

        public void UpdateRefund(Refund refund)
        {
            _context.Refunds.Update(refund);
            _context.SaveChanges();
        }
    }
}
