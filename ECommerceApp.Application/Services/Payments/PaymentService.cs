using AutoMapper;
using AutoMapper.QueryableExtensions;
using ECommerceApp.Application.DTO;
using ECommerceApp.Application.Exceptions;
using ECommerceApp.Application.Services.Currencies;
using ECommerceApp.Application.Services.Customers;
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
    internal class PaymentService : IPaymentService
    {
        private readonly IMapper _mapper;
        private readonly IPaymentRepository _repo;
        private readonly IOrderRepository _orderRepository;
        private readonly ICustomerService _customerService;
        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly IPaymentHandler _paymentHandler;

        public PaymentService(IPaymentRepository paymentRepository, IMapper mapper, IOrderRepository orderRepository, ICustomerService customerService, IHttpContextAccessor httpContextAccessor, IPaymentHandler paymentHandler)
        {
            _mapper = mapper;
            _repo = paymentRepository;
            _orderRepository = orderRepository;
            _customerService = customerService;
            _httpContextAccessor = httpContextAccessor;
            _paymentHandler = paymentHandler;
        }

        public int AddPayment(AddPaymentDto model)
        {
            if (model is null)
            {
                throw new BusinessException($"{typeof(AddPaymentDto).Name} cannot be null");
            }
            var order = _orderRepository.GetById(model.OrderId) ??
                throw new BusinessException($"Order with id '{model.OrderId}' was not found"); ;
            var paymentId = _paymentHandler.CreatePayment(model, order);
            order.IsPaid = true;
            order.PaymentId = paymentId;
            _orderRepository.Update(order);
            return paymentId;
        }

        public int PaidIssuedPayment(PaymentVm model)
        {
            if (model is null)
            {
                throw new BusinessException($"{typeof(PaymentVm).Name} cannot be null");
            }

            var order = _orderRepository.GetById(model.OrderId) ??
                throw new BusinessException($"Order with id '{model.OrderId}' was not found");
            var paymentId = _paymentHandler.PayIssuedPayment(model, order);
            _orderRepository.Update(order);
            return paymentId;
        }

        public bool DeletePayment(int id)
        {
            var payment = _repo.GetById(id);
            var order = _orderRepository.GetById(payment.OrderId) ??
                throw new BusinessException($"Order with id '{payment.OrderId}' was not found"); ;
            order.IsPaid = false;
            order.PaymentId = null;
            _orderRepository.Update(order);
            return _repo.Delete(payment);
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

            var order = _orderRepository.GetById(orderId);
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

        private static DateTime SetFormat(DateTime dateTime)
        {
            return new DateTime(dateTime.Year, dateTime.Month, dateTime.Day, dateTime.Hour, dateTime.Minute, dateTime.Second);
        }
    }
}
