using ECommerceApp.Application.Interfaces;
using ECommerceApp.Application.ViewModels.Refund;
using ECommerceApp.Domain.Interface;
using ECommerceApp.Domain.Model;
using System.Collections.Generic;

namespace ECommerceApp.Application.Services.Refunds
{
    public interface IRefundService : IAbstractService<RefundVm, IRefundRepository, Refund>
    {
        int AddRefund(RefundVm refund);
        RefundDetailsVm GetRefundDetails(int id);
        RefundVm GetRefundById(int id);
        void UpdateRefund(RefundVm refundVm);
        IEnumerable<RefundVm> GetRefunds();
        ListForRefundVm GetRefunds(int pageSize, int pageNo, string searchString);
        bool RefundExists(int id);
        void DeleteRefund(int id);
        bool SameReasonNotExists(string reasonRefund);
    }
}
