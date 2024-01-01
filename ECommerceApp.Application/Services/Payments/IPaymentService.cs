using ECommerceApp.Application.Interfaces;
using ECommerceApp.Application.ViewModels.Payment;
using ECommerceApp.Domain.Interface;
using ECommerceApp.Domain.Model;
using System;
using System.Collections.Generic;
using System.Linq.Expressions;

namespace ECommerceApp.Application.Services.Payments
{
    public interface IPaymentService : IAbstractService<PaymentVm, IPaymentRepository, Payment>
    {
        int AddPayment(PaymentVm model);
        int PaidIssuedPayment(PaymentVm model);
        PaymentVm GetPaymentById(int id);
        void UpdatePayment(PaymentVm model);
        IEnumerable<PaymentVm> GetPayments(Expression<Func<Payment, bool>> expression);
        IEnumerable<PaymentVm> GetPaymentsForUser(Expression<Func<Payment, bool>> expression, string userId);
        ListForPaymentVm GetPayments(int pageSize, int pageNo, string searchString);
        bool PaymentExists(int id);
        void DeletePayment(int id);
        PaymentDetailsVm GetPaymentDetails(int id);
        PaymentVm InitPayment(int orderId);
    }
}
