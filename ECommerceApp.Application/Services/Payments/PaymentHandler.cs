using ECommerceApp.Application.DTO;
using ECommerceApp.Application.Exceptions;
using ECommerceApp.Application.Services.Currencies;
using ECommerceApp.Application.ViewModels.Payment;
using ECommerceApp.Domain.Interface;
using ECommerceApp.Domain.Model;
using System;

namespace ECommerceApp.Application.Services.Payments
{
    internal sealed class PaymentHandler : IPaymentHandler
    {
        private readonly IPaymentRepository _paymentRepository;
        private readonly ICurrencyRateService _currencyRateService;

        public PaymentHandler(IPaymentRepository paymentRepository, ICurrencyRateService currencyRateService)
        {
            _paymentRepository = paymentRepository;
            _currencyRateService = currencyRateService;
        }

        public int CreatePayment(AddPaymentDto addPaymentDto, Order order)
        {
            if (addPaymentDto is null)
            {
                throw new BusinessException($"{typeof(AddPaymentDto).Name} cannot be null");
            }

            if (order.IsPaid)
            {
                throw new BusinessException($"Order with id '{order.Id}' has alredy been paid");
            }

            var payment = new Payment()
            {
                Number = Guid.NewGuid().ToString(),
                State = PaymentState.Paid,
                CurrencyId = addPaymentDto.CurrencyId,
                DateOfOrderPayment = DateTime.Now,
                Cost = CalculateCost(order.Cost, addPaymentDto.CurrencyId),
                CustomerId = order.CustomerId,
                OrderId = order.Id
            };
            var paymentId = _paymentRepository.AddPayment(payment);
            order.IsPaid = true;
            order.PaymentId = paymentId;
            return paymentId;
        }

        public void HandlePaymentChangesOnOrder(PaymentInfoDto dto, Order order)
        {
            if (order is null)
            {
                throw new BusinessException($"{typeof(Order).Name} cannot be null");
            }

            if (dto is null && !order.PaymentId.HasValue)
            {
                return;
            }

            if (dto is not null && dto.Id != order.PaymentId)
            {
                throw new BusinessException($"Cannot assign existed payment with id '{dto.Id}'");
            }

            if (dto is null && order.PaymentId.HasValue)
            {
                _paymentRepository.Delete(order.PaymentId.Value);
                order.PaymentId = null;
                order.Payment = null;
                return;
            }

            CreatePayment(new AddPaymentDto
            {
                CurrencyId = dto.CurrencyId,
                OrderId = order.Id
            }, order);
        }

        public int PaidIssuedPayment(PaymentVm model, Order order)
        {
            if (model is null)
            {
                throw new BusinessException($"{typeof(PaymentVm).Name} cannot be null");
            }

            var payment = _paymentRepository.GetById(model.Id)
                ?? throw new BusinessException($"Payment with id '{model.Id}' was not found");
            payment.State = PaymentState.Paid;
            payment.CurrencyId = model.CurrencyId;
            payment.Cost = CalculateCost(payment.Cost, model.CurrencyId);
            payment.DateOfOrderPayment = DateTime.Now;
            payment.CustomerId = order.CustomerId;
            _paymentRepository.Update(payment);
            order.IsPaid = true;
            order.PaymentId = payment.Id;
            return payment.Id;
        }

        private decimal CalculateCost(decimal cost, int currencyId)
        {
            var rate = _currencyRateService.GetLatestRate(currencyId);
            var calculatedCost = cost / rate.Rate;
            return calculatedCost;
        }
    }
}
