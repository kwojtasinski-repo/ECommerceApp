using ECommerceApp.Domain.Model;
using System.Collections.Generic;

namespace ECommerceApp.Domain.Interface
{
    public interface IRefundRepository : IGenericRepository<Refund>
    {
        void DeleteRefund(int refundId);
        int AddRefund(Refund refund);
        Refund GetRefundById(int refundId);
        List<Refund> GetAllRefunds();
        List<Refund> GetAllRefunds(int pageSize, int pageNo, string searchString);
        void UpdateRefund(Refund refund);
        bool ExistsByReason(string reasonRefund);
        int GetCountBySearchString(string searchString);
        Refund GetDetailsById(int id);
    }
}
