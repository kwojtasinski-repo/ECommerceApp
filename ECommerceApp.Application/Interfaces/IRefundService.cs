using ECommerceApp.Application.ViewModels.Order;
using ECommerceApp.Application.ViewModels.Refund;
using ECommerceApp.Domain.Interface;
using ECommerceApp.Domain.Model;
using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Text;

namespace ECommerceApp.Application.Interfaces
{
    public interface IRefundService : IAbstractService<RefundVm, IRefundRepository, Refund>
    {
        int AddRefund(RefundVm refund);
        RefundDetailsVm GetRefundDetails(int id);
        RefundVm GetRefundById(int id);
        void UpdateRefund(RefundVm refundVm);
        IEnumerable<RefundVm> GetRefunds(Expression<Func<Refund, bool>> expression);
        ListForRefundVm GetRefunds(int pageSize, int pageNo, string searchString);
        bool RefundExists(int id);
        void DeleteRefund(int id);
    }
}
