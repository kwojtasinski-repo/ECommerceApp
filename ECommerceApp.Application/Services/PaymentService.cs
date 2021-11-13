using AutoMapper;
using AutoMapper.QueryableExtensions;
using ECommerceApp.Application.Abstracts;
using ECommerceApp.Application.Exceptions;
using ECommerceApp.Application.Interfaces;
using ECommerceApp.Application.ViewModels.Order;
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
        public PaymentService(IPaymentRepository paymentRepository, IMapper mapper) : base(paymentRepository, mapper)
        { }

        public int AddPayment(PaymentVm model)
        {
            if (model.Id != 0)
            {
                throw new BusinessException("When adding object Id should be equals 0");
            }

            var payment = _mapper.Map<Payment>(model);
            var id = _repo.AddPayment(payment);
            return id;
        }

        public void DeletePayment(int id)
        {
            _repo.DeletePayment(id);
        }

        public PaymentDetailsVm GetPaymentDetails(int id)
        {
            var payment = _repo.GetAll().Include(o => o.Order).Include(c => c.Customer).Where(p => p.Id == id).FirstOrDefault();
            var paymentVm = _mapper.Map<PaymentDetailsVm>(payment);
            return paymentVm;
        }

        public PaymentVm GetPaymentById(int id)
        {
            var payment = Get(id);
            return payment;
        }

        public IEnumerable<PaymentVm> GetPayments(Expression<Func<Payment, bool>> expression)
        {
            var payments = _repo.GetAll().Where(expression)
               .ProjectTo<PaymentVm>(_mapper.ConfigurationProvider);
            var paymentsToShow = payments.ToList();

            return paymentsToShow;
        }

        public ListForPaymentVm GetPayments(int pageSize, int pageNo, string searchString)
        {
            var payments = _repo.GetAllPayments().Where(p => p.Number.ToString().StartsWith(searchString))
                            .ProjectTo<PaymentForListVm>(_mapper.ConfigurationProvider)
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
            var payment = _mapper.Map<Payment>(model);
            if (payment != null)
            {
                _repo.UpdatePayment(payment);
            }
        }
    }
}
