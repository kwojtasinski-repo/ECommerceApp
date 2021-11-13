using ECommerceApp.Application.ViewModels.Order;
using ECommerceApp.Domain.Interface;
using ECommerceApp.Domain.Model;
using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Text;

namespace ECommerceApp.Application.Interfaces
{
    public interface IPaymentService : IAbstractService<PaymentVm, IPaymentRepository, Payment>
    {
        int AddPayment(PaymentVm model);
        PaymentDetailsVm GetPaymentDetails(int id);
        PaymentVm GetPaymentById(int id);
        void UpdatePayment(PaymentVm model);
        IEnumerable<PaymentVm> GetPayments(Expression<Func<Payment, bool>> expression);
        ListForPaymentVm GetPayments(int pageSize, int pageNo, string searchString);
        bool PaymentExists(int id);
        void DeletePayment(int id);
    }
}
