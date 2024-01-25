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
        private readonly ICurrencyRepository _currencyRepository;

        public PaymentHandler(IPaymentRepository paymentRepository, ICurrencyRateService currencyRateService, ICurrencyRepository currencyRepository)
        {
            _paymentRepository = paymentRepository;
            _currencyRateService = currencyRateService;
            _currencyRepository = currencyRepository;
        }

        public int CreatePayment(AddPaymentDto addPaymentDto, Order order)
        {
            if (order is null)
            {
                throw new BusinessException($"{typeof(Order).Name} cannot be null");
            }

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

            if (dto is null && order.PaymentId.HasValue)
            {
                _paymentRepository.DeletePayment(order.PaymentId.Value);
                order.PaymentId = null;
                order.Payment = null;
                order.IsPaid = false;
                return;
            }

            if (dto is null)
            {
                throw new BusinessException($"{typeof(PaymentInfoDto).Name} cannot be null");
            }

            if (dto.Id.HasValue && order.PaymentId.HasValue && dto.Id == order.PaymentId.Value)
            {
                return;
            }

            if (!dto.Id.HasValue && order.PaymentId.HasValue)
            {
                throw new BusinessException($"Cannot pay for paid order with id '{order.Id}'");
            }

            if (dto.Id.HasValue && !order.PaymentId.HasValue)
            {
                throw new BusinessException($"Cannot assign existed payment with id '{dto.Id}'");
            }

            if (dto.Id.HasValue && order.PaymentId.HasValue && dto.Id != order.PaymentId)
            {
                throw new BusinessException("Overriding payment id on order is not allowed");
            }

            CreatePayment(new AddPaymentDto
            {
                CurrencyId = dto.CurrencyId,
                OrderId = order.Id
            }, order);
        }

        public int PayIssuedPayment(PaymentVm model, Order order)
        {
            if (model is null)
            {
                throw new BusinessException($"{typeof(PaymentVm).Name} cannot be null");
            }

            if (order is null)
            {
                throw new BusinessException($"{typeof(Order).Name} cannot be null");
            }

            if (order.IsPaid)
            {
                throw new BusinessException($"Order with id '{order.Id}' has alredy been paid");
            }

            if (model.State == PaymentState.Paid)
            {
                throw new BusinessException($"Payment with id '{model.Id}' was already paid");
            }

            var payment = _paymentRepository.GetPaymentById(model.Id)
                ?? throw new BusinessException($"Payment with id '{model.Id}' was not found");
            payment.State = PaymentState.Paid;
            payment.CurrencyId = model.CurrencyId;
            payment.Currency = _currencyRepository.GetById(model.CurrencyId)
                ?? throw new BusinessException($"Currency with id '{model.CurrencyId}' was not found");
            payment.Cost = CalculateCost(payment.Cost, model.CurrencyId);
            payment.DateOfOrderPayment = DateTime.Now;
            payment.CustomerId = order.CustomerId;
            payment.Order = order;
            payment.OrderId = order.Id;
            _paymentRepository.UpdatePayment(payment);
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
