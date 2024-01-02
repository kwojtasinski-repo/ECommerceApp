using AutoMapper;
using AutoMapper.QueryableExtensions;
using ECommerceApp.Application.Abstracts;
using ECommerceApp.Application.DTO;
using ECommerceApp.Application.Exceptions;
using ECommerceApp.Application.Services.Currencies;
using ECommerceApp.Application.Services.Customers;
using ECommerceApp.Application.Services.Orders;
using ECommerceApp.Application.ViewModels.Payment;
using ECommerceApp.Domain.Interface;
using ECommerceApp.Domain.Model;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;

namespace ECommerceApp.Application.Services.Payments
{
    public class PaymentService : AbstractService<PaymentVm, IPaymentRepository, Payment>, IPaymentService
    {
        private readonly IOrderService _orderService;
        private readonly ICustomerService _customerService;
        private readonly ICurrencyRateService _currencyRateService;
        private readonly IHttpContextAccessor _httpContextAccessor;

        public PaymentService(IPaymentRepository paymentRepository, IMapper mapper, IOrderService orderService, ICustomerService customerService, ICurrencyRateService currencyRateService, IHttpContextAccessor httpContextAccessor) : base(paymentRepository, mapper)
        {
            _orderService = orderService;
            _customerService = customerService;
            _currencyRateService = currencyRateService;
            _httpContextAccessor = httpContextAccessor;
        }

        public int AddPayment(AddPaymentDto model)
        {
            if (model is null)
            {
                throw new BusinessException($"{typeof(AddPaymentDto).Name} cannot be null");
            }

            var order = _orderService.Get(model.OrderId) ??
                throw new BusinessException($"Order with id '{model.OrderId}' was not found");
            if (order.IsPaid)
            {
                throw new BusinessException($"Order with id '{model.OrderId}' has alredy been paid");
            }

            var payment = new Payment()
            {
                Number = Guid.NewGuid().ToString(),
                State = PaymentState.Paid,
                CurrencyId = model.CurrencyId,
                DateOfOrderPayment = DateTime.Now,
                Cost = CalculateCost(order.Cost, model.CurrencyId),
                CustomerId = order.CustomerId,
                OrderId = order.Id
            };
            _repo.AddPayment(payment);
            order.IsPaid = true;
            order.PaymentId = payment.Id;
            _orderService.Update(order);
            return payment.Id;
        }

        public int PaidIssuedPayment(PaymentVm model)
        {
            if (model is null)
            {
                throw new BusinessException($"{typeof(PaymentVm).Name} cannot be null");
            }

            var payment = _repo.GetById(model.Id)
                ?? throw new BusinessException($"Payment with id '{model.Id}' was not found");
            var order = _orderService.Get(payment.OrderId) ??
                throw new BusinessException($"Order with id '{payment.Id}' was not found");
            payment.State = PaymentState.Paid;
            payment.CurrencyId = model.CurrencyId;
            payment.Cost = CalculateCost(payment.Cost, model.CurrencyId);
            payment.DateOfOrderPayment = DateTime.Now;
            payment.CustomerId = order.CustomerId;
            _repo.Update(payment);
            order.IsPaid = true;
            order.PaymentId = payment.Id;
            _orderService.Update(order);
            return payment.Id;
        }

        public void DeletePayment(int id)
        {
            var payment = _repo.GetById(id);
            var order = _orderService.Get(payment.OrderId);
            order.IsPaid = false;
            order.PaymentId = null;
            _orderService.Update(order);
            _repo.Delete(payment);
        }

        public PaymentVm GetPaymentById(int id)
        {
            var payment = _repo.GetAll().Include(p => p.Order).Include(c => c.Customer).Where(p => p.Id == id).FirstOrDefault();
            var paymentVm = _mapper.Map<PaymentVm>(payment);
            return paymentVm;
        }

        public IEnumerable<PaymentDto> GetPayments()
        {
            var payments = _repo.GetAll()
                .Include(p => p.Currency)
                .Select(p => new Payment
                {
                    Id = p.Id,
                    DateOfOrderPayment = p.DateOfOrderPayment,
                    Number = p.Number,
                    Cost = p.Cost,
                    CurrencyId = p.CurrencyId,
                    CustomerId = p.CustomerId,
                    OrderId = p.OrderId,
                    Currency = new Currency { Id = p.CurrencyId, Code = p.Currency.Code },
                })
               .ProjectTo<PaymentDto>(_mapper.ConfigurationProvider);
            var paymentsToShow = payments.ToList();

            return paymentsToShow;
        }

        public IEnumerable<PaymentDto> GetUserPayments(string userId)
        {
            var payments = _repo.GetAll()
                .Include(c => c.Customer)
                .Include(p => p.Currency)
                .Where(p => p.Customer.UserId == userId)
                .Select(p => new Payment
                {
                    Id = p.Id,
                    DateOfOrderPayment = p.DateOfOrderPayment,
                    Number = p.Number,
                    Cost = p.Cost,
                    CurrencyId = p.CurrencyId,
                    CustomerId = p.CustomerId,
                    OrderId = p.OrderId,
                    Currency = new Currency { Id = p.CurrencyId, Code = p.Currency.Code },
                })
                .ProjectTo<PaymentDto>(_mapper.ConfigurationProvider);
            var paymentsToShow = payments.ToList();

            return paymentsToShow;
        }

        public ListForPaymentVm GetPayments(int pageSize, int pageNo, string searchString)
        {
            var payments = _repo.GetAllPayments()
                            .Include(p => p.Currency)
                            .Where(p => p.Number.ToString().StartsWith(searchString))
                            .Select(p => new Payment
                            {
                                Id = p.Id,
                                DateOfOrderPayment = p.DateOfOrderPayment,
                                Number = p.Number,
                                Cost = p.Cost,
                                CurrencyId = p.CurrencyId,
                                CustomerId = p.CustomerId,
                                OrderId = p.OrderId,
                                Currency = new Currency { Id = p.CurrencyId, Code = p.Currency.Code },
                            })
                            .ProjectTo<PaymentDto>(_mapper.ConfigurationProvider)
                            .ToList();
            var paymentsToShow = payments.Skip(pageSize * (pageNo - 1)).Take(pageSize).ToList();

            var paymentsList = new ListForPaymentVm()
            {
                PageSize = pageSize,
                CurrentPage = pageNo,
                SearchString = searchString,
                Payments = paymentsToShow,
                Count = payments.Count
            };

            return paymentsList;
        }

        public bool PaymentExists(int id)
        {
            var payment = _repo.GetById(id);
            var exists = payment != null;

            if (exists)
            {
                _repo.DetachEntity(payment);
            }

            return exists;
        }

        public void UpdatePayment(PaymentVm model)
        {
            if (model is null)
            {
                throw new BusinessException($"{typeof(PaymentVm).Name} cannot be null");
            }

            var payment = _mapper.Map<Payment>(model);
            if (payment != null)
            {
                _repo.UpdatePayment(payment);
            }
        }

        public PaymentDetailsVm GetPaymentDetails(int id)
        {
            var userId = _httpContextAccessor.GetUserId();
            var payment = _repo.GetAll().Include(c => c.Customer).ThenInclude(a => a.Addresses)
                                        .Include(o => o.Order)
                                        .Where(p => p.Customer.UserId == userId && p.Id == id).FirstOrDefault();
            var paymentVm = _mapper.Map<PaymentDetailsVm>(payment);
            return paymentVm;
        }

        public PaymentVm InitPayment(int orderId)
        {
            var paymentExists = _repo.GetPaymentByOrderId(orderId);
            if (paymentExists is not null)
            {
                var customerInformation = _customerService.GetCustomerInformationById(paymentExists.CustomerId);
                return new PaymentVm()
                {
                    Id = paymentExists.Id,
                    CurrencyId = paymentExists.CurrencyId,
                    OrderId = paymentExists.OrderId,
                    Number = paymentExists.Number,
                    DateOfOrderPayment = SetFormat(DateTime.Now),
                    CustomerId = paymentExists.CustomerId,
                    OrderNumber = _orderService.GetOrderNumber(orderId),
                    CustomerName = customerInformation.Information,
                    OrderCost = paymentExists.Cost
                };
            }

            var order = _orderService.Get(orderId);
            var customer = _customerService.GetCustomerInformationById(order.CustomerId);
            var payment = new Payment
            {
                Cost = order.Cost,
                OrderId = orderId,
                Number = Guid.NewGuid().ToString(),
                DateOfOrderPayment = DateTime.Now,
                State = PaymentState.Issued,
                CustomerId = customer.Id,
                CurrencyId = order.CurrencyId
            };
            _repo.AddPayment(payment);
            var paymentVm = new PaymentVm()
            {
                Id = payment.Id,
                OrderId = order.Id,
                Number = payment.Number,
                DateOfOrderPayment = SetFormat(payment.DateOfOrderPayment),
                CustomerId = order.CustomerId,
                OrderNumber = order.Number,
                CustomerName = customer.Information,
                OrderCost = order.Cost
            };

            return paymentVm;
        }

        private decimal CalculateCost(decimal cost, int currencyId)
        {
            var rate = _currencyRateService.GetLatestRate(currencyId);
            var calculatedCost = cost / rate.Rate;
            return calculatedCost;
        }

        private static DateTime SetFormat(DateTime dateTime)
        {
            return new DateTime(dateTime.Year, dateTime.Month, dateTime.Day, dateTime.Hour, dateTime.Minute, dateTime.Second);
        }
    }
}
