using ECommerceApp.Application.DTO;
using ECommerceApp.Application.ViewModels.Payment;
using ECommerceApp.Domain.Model;

namespace ECommerceApp.Application.Services.Payments
{
    internal interface IPaymentHandler
    {
        int CreatePayment(AddPaymentDto addPaymentDto, Order order);
        int PaidIssuedPayment(PaymentVm model, Order order);
        void HandlePaymentChangesOnOrder(PaymentInfoDto dto, Order order);
    }
}
