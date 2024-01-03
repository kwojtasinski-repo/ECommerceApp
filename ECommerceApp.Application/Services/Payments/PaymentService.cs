using AutoMapper;
using AutoMapper.QueryableExtensions;
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

namespace ECommerceApp.Application.Services.Payments
{
    public class PaymentService : IPaymentService
    {
        private readonly IMapper _mapper;
        private readonly IPaymentRepository _repo;
        private readonly IOrderService _orderService;
        private readonly ICustomerService _customerService;
        private readonly ICurrencyRateService _currencyRateService;
        private readonly IHttpContextAccessor _httpContextAccessor;

        public PaymentService(IPaymentRepository paymentRepository, IMapper mapper, IOrderService orderService, ICustomerService customerService, ICurrencyRateService currencyRateService, IHttpContextAccessor httpContextAccessor)
        {
            _mapper = mapper;
            _repo = paymentRepository;
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
            var payment = _repo.GetPaymentById(id);
            if (payment is null)
            {
                return null;
            }

            var vm = _mapper.Map<PaymentVm>(payment);
            vm.DateOfOrderPayment = SetFormat(DateTime.Now);

            return vm;
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
                    State = p.State,
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
                    State = p.State,
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
                            .Where(p => p.Number.StartsWith(searchString))
                            .Select(p => new Payment
                            {
                                Id = p.Id,
                                DateOfOrderPayment = p.DateOfOrderPayment,
                                Number = p.Number,
                                Cost = p.Cost,
                                State = p.State,
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

        public PaymentDetailsDto GetPaymentDetails(int id)
        {
            var userId = _httpContextAccessor.GetUserId();
            var payment = _repo.GetAll().Include(c => c.Customer)
                                        .Include(o => o.Order)
                                        .Include(c => c.Currency)
                                        .Where(p => p.Customer.UserId == userId && p.Id == id)
                                        .Select(p => new Payment
                                        {
                                            Id = p.Id,
                                            Cost = p.Cost,
                                            Number = p.Number,
                                            State = p.State,
                                            DateOfOrderPayment = p.DateOfOrderPayment,
                                            CurrencyId = p.CurrencyId,
                                            CustomerId = p.CustomerId,
                                            OrderId = p.OrderId,
                                            Customer = new Customer
                                            {
                                                Id = p.CustomerId,
                                                FirstName = p.Customer.FirstName,
                                                LastName = p.Customer.LastName,
                                            },
                                            Order = new Order
                                            {
                                                Id = p.OrderId,
                                                Number = p.Order.Number
                                            },
                                            Currency = new Currency
                                            {
                                                Id = p.CurrencyId,
                                                Code = p.Currency.Code
                                            }
                                        })
                                        .FirstOrDefault();
            var paymentVm = _mapper.Map<PaymentDetailsDto>(payment);
            return paymentVm;
        }

        public PaymentVm InitPayment(int orderId)
        {
            var paymentExists = _repo.GetPaymentByOrderId(orderId);
            if (paymentExists is not null)
            {
                var vm = _mapper.Map<PaymentVm>(paymentExists);
                vm.DateOfOrderPayment = SetFormat(DateTime.Now);

                return vm;
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
                CurrencyId = order.CurrencyId,
                CustomerId = order.CustomerId,
                OrderNumber = order.Number,
                CustomerName = customer.Information,
                Cost = order.Cost
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
