using ECommerceApp.Domain.Model;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace ECommerceApp.Domain.Interface
{
    public interface IRefundRepository : IGenericRepository<Refund>
    {
        void DeleteRefund(int refundId);
        int AddRefund(Refund refund);
        Refund GetRefundById(int refundId);
        IQueryable<Refund> GetAllRefunds();
        void UpdateRefund(Refund refund);
    }
}
