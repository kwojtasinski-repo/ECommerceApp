using AutoMapper;
using ECommerceApp.Application.DTO;
using ECommerceApp.Application.Exceptions;
using ECommerceApp.Application.Services.Customers;
using ECommerceApp.Application.ViewModels.Payment;
using ECommerceApp.Domain.Interface;
using ECommerceApp.Domain.Model;
using Microsoft.AspNetCore.Http;
using System;
using System.Collections.Generic;

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
            var order = _orderRepository.GetOrderById(model.OrderId) ??
                throw new BusinessException($"Order with id '{model.OrderId}' was not found");
            var paymentId = _paymentHandler.CreatePayment(model, order);
            order.IsPaid = true;
            order.PaymentId = paymentId;
            _orderRepository.UpdatedOrder(order);
            return paymentId;
        }

        public int PaidIssuedPayment(PaymentVm model)
        {
            if (model is null)
            {
                throw new BusinessException($"{typeof(PaymentVm).Name} cannot be null");
            }

            var order = _orderRepository.GetOrderById(model.OrderId) ??
                throw new BusinessException($"Order with id '{model.OrderId}' was not found");
            var paymentId = _paymentHandler.PayIssuedPayment(model, order);
            _orderRepository.UpdatedOrder(order);
            return paymentId;
        }

        public bool DeletePayment(int id)
        {
            var payment = _repo.GetPaymentById(id);
            var order = _orderRepository.GetOrderById(payment.OrderId) ??
                throw new BusinessException($"Order with id '{payment.OrderId}' was not found");
            order.IsPaid = false;
            order.PaymentId = null;
            _orderRepository.UpdatedOrder(order);
            return _repo.DeletePayment(payment.Id);
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
            return _mapper.Map<List<PaymentDto>>(_repo.GetAllPayments());
        }

        public IEnumerable<PaymentDto> GetUserPayments(string userId)
        {
            return _mapper.Map<List<PaymentDto>>(_repo.GetAllUserPayments(userId));
        }

        public ListForPaymentVm GetPayments(int pageSize, int pageNo, string searchString)
        {
            var payments = _mapper.Map<List<PaymentDto>>(_repo.GetAllPayments(pageSize, pageNo, searchString));

            var paymentsList = new ListForPaymentVm()
            {
                PageSize = pageSize,
                CurrentPage = pageNo,
                SearchString = searchString,
                Payments = payments,
                Count = _repo.GetCountBySearchString(searchString)
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
            return _mapper.Map<PaymentDetailsDto>(_repo.GetPaymentDetailsByIdAndUserId(id, userId));
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

            var order = _orderRepository.GetOrderById(orderId);
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
