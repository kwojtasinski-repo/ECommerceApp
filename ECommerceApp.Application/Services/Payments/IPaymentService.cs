using ECommerceApp.Application.DTO;
using ECommerceApp.Application.ViewModels.Payment;
using System.Collections.Generic;

namespace ECommerceApp.Application.Services.Payments
{
    public interface IPaymentService
    {
        int AddPayment(AddPaymentDto model);
        int PaidIssuedPayment(PaymentVm model);
        PaymentVm GetPaymentById(int id);
        void UpdatePayment(PaymentVm model);
        IEnumerable<PaymentDto> GetPayments();
        IEnumerable<PaymentDto> GetUserPayments(string userId);
        ListForPaymentVm GetPayments(int pageSize, int pageNo, string searchString);
        bool PaymentExists(int id);
        bool DeletePayment(int id);
        PaymentDetailsDto GetPaymentDetails(int id);
        PaymentVm InitPayment(int orderId);
    }
}
