using AutoMapper;
using AutoMapper.QueryableExtensions;
using ECommerceApp.Application.Abstracts;
using ECommerceApp.Application.Exceptions;
using ECommerceApp.Application.Interfaces;
using ECommerceApp.Application.ViewModels.Order;
using ECommerceApp.Application.ViewModels.Payment;
using ECommerceApp.Domain.Interface;
using ECommerceApp.Domain.Model;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Text;

namespace ECommerceApp.Application.Services
{
    public class PaymentService : AbstractService<PaymentVm, IPaymentRepository, Payment>, IPaymentService
    {
        private readonly IOrderService _orderService;
        private readonly ICustomerService _customerService;
        private readonly ICurrencyRateService _currencyRateService;

        public PaymentService(IPaymentRepository paymentRepository, IMapper mapper, IOrderService orderService, ICustomerService customerService, ICurrencyRateService currencyRateService) : base(paymentRepository, mapper)
        {
            _orderService = orderService;
            _customerService = customerService;
            _currencyRateService = currencyRateService;
        }

        public int AddPayment(PaymentVm model)
        {
            if (model is null)
            {
                throw new BusinessException($"{typeof(PaymentVm).Name} cannot be null");
            }

            if (model.Id != 0)
            {
                throw new BusinessException("When adding object Id should be equals 0");
            }

            var payment = _mapper.Map<Payment>(model);
            var id = _repo.AddPayment(payment);
            var order = _orderService.Get(payment.OrderId);
            order.IsPaid = true;
            order.PaymentId = id;
            order.CurrencyId = model.CurrencyId;
            order.Cost = CalculateCost(order.Cost, model.CurrencyId);
            _orderService.Update(order);
            return id;
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

        public PaymentDetailsVm GetPaymentDetails(int id)
        {
            var payment = _repo.GetAll().Include(o => o.Order).Include(c => c.Customer).Where(p => p.Id == id).FirstOrDefault();
            var paymentVm = _mapper.Map<PaymentDetailsVm>(payment);
            return paymentVm;
        }

        public PaymentVm GetPaymentById(int id)
        {
            var payment = _repo.GetAll().Include(p => p.Order).Include(c => c.Customer).Where(p => p.Id == id).FirstOrDefault();
            var paymentVm = _mapper.Map<PaymentVm>(payment);
            return paymentVm;
        }

        public IEnumerable<PaymentVm> GetPayments(Expression<Func<Payment, bool>> expression)
        {
            var payments = _repo.GetAll().Where(expression)
               .ProjectTo<PaymentVm>(_mapper.ConfigurationProvider);
            var paymentsToShow = payments.ToList();

            return paymentsToShow;
        }

        public IEnumerable<PaymentVm> GetPaymentsForUser(Expression<Func<Payment, bool>> expression, string userId)
        {
            var payments = _repo.GetAll()
                .Include(c => c.Customer)
                .Where(expression)
                .Where(p => p.Customer.UserId == userId)
                .ProjectTo<PaymentVm>(_mapper.ConfigurationProvider);
            var paymentsToShow = payments.ToList();

            return paymentsToShow;
        }

        public ListForPaymentVm GetPayments(int pageSize, int pageNo, string searchString)
        {
            var payments = _repo.GetAllPayments().Where(p => p.Number.ToString().StartsWith(searchString))
                            .ProjectTo<PaymentVm>(_mapper.ConfigurationProvider)
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

        public PaymentDetailsVm GetPaymentDetails(int id, string userId)
        {
            var payment = _repo.GetAll().Include(c => c.Customer).ThenInclude(a => a.Addresses)
                                        .Include(o => o.Order)
                                        .Where(p => p.Customer.UserId == userId && p.Id == id).FirstOrDefault();
            var paymentVm = _mapper.Map<PaymentDetailsVm>(payment);
            return paymentVm;
        }

        public PaymentVm InitPayment(int orderId)
        {
            Random random = new Random();
            var order = _orderService.GetOrderById(orderId);
            var customer = _customerService.GetCustomerInformationById(order.CustomerId);
            var payment = new PaymentVm()
            {
                OrderId = order.Id,
                Number = random.Next(1, 1000),
                DateOfOrderPayment = System.DateTime.Now,
                CustomerId = order.CustomerId,
                OrderNumber = order.Number,
                CustomerName = customer.Information,
                OrderCost = order.Cost
            };

            return payment;
        }

        private decimal CalculateCost(decimal cost, int currencyId)
        {
            var rate = _currencyRateService.GetLatestRate(currencyId);
            var calculatedCost = cost / rate.Rate;
            return calculatedCost;
        }
    }
}
