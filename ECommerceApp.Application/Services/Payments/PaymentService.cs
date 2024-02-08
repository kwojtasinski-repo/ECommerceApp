using AutoMapper;
using ECommerceApp.Application.DTO;
using ECommerceApp.Application.Exceptions;
using ECommerceApp.Application.Interfaces;
using ECommerceApp.Application.Permissions;
using ECommerceApp.Application.Services.Customers;
using ECommerceApp.Application.ViewModels.Payment;
using ECommerceApp.Domain.Interface;
using ECommerceApp.Domain.Model;
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
        private readonly IUserContext _userContext;
        private readonly IPaymentHandler _paymentHandler;

        public PaymentService(IPaymentRepository paymentRepository, IMapper mapper, IOrderRepository orderRepository, ICustomerService customerService, IUserContext userContext, IPaymentHandler paymentHandler)
        {
            _mapper = mapper;
            _repo = paymentRepository;
            _orderRepository = orderRepository;
            _customerService = customerService;
            _userContext = userContext;
            _paymentHandler = paymentHandler;
        }

        public int AddPayment(AddPaymentDto model)
        {
            if (model is null)
            {
                throw new BusinessException($"{typeof(AddPaymentDto).Name} cannot be null");
            }
            var order = _orderRepository.GetOrderById(model.OrderId) ??
                throw new BusinessException($"Order with id '{model.OrderId}' was not found", "orderNotFound", new Dictionary<string, string> { { "id", $"{model.OrderId}" } });
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

            var order = _orderRepository.GetOrderById(model.OrderId);
            if (order is null)
            {
                return default;
            }

            var paymentId = _paymentHandler.PayIssuedPayment(model, order);
            _orderRepository.UpdatedOrder(order);
            return paymentId;
        }

        public bool DeletePayment(int id)
        {
            var payment = _repo.GetPaymentById(id);
            if (payment is null)
            {
                return false;
            }

            var order = _orderRepository.GetOrderById(payment.OrderId) ??
                throw new BusinessException($"Order with id '{payment.OrderId}' was not found", "orderNotFound", new Dictionary<string, string> { { "id", $"{payment.OrderId}" } });
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

        public bool UpdatePayment(PaymentVm model)
        {
            if (model is null)
            {
                throw new BusinessException($"{typeof(PaymentVm).Name} cannot be null");
            }

            if (!_repo.ExistsBydId(model.Id))
            {
                return false;
            }

            var payment = _mapper.Map<Payment>(model);
            _repo.UpdatePayment(payment);
            return true;
        }

        public PaymentDetailsDto GetPaymentDetails(int id)
        {
            if (!UserPermissions.Roles.MaintenanceRoles.Contains(_userContext.Role)
                && !_repo.ExistsByIdAndUserId(id, _userContext.UserId))
            {
                return null;
            }
            return _mapper.Map<PaymentDetailsDto>(_repo.GetPaymentDetailsByIdAndUserId(id, _userContext.UserId));
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
