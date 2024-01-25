using ECommerceApp.Domain.Model;
using System.Linq;

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
