﻿using AutoMapper;
using AutoMapper.QueryableExtensions;
using ECommerceApp.Application.Abstracts;
using ECommerceApp.Application.Exceptions;
using ECommerceApp.Application.Services.Orders;
using ECommerceApp.Application.ViewModels.Order;
using ECommerceApp.Application.ViewModels.Refund;
using ECommerceApp.Domain.Interface;
using ECommerceApp.Domain.Model;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Text;

namespace ECommerceApp.Application.Services.Refunds
{
    public class RefundService : AbstractService<RefundVm, IRefundRepository, Refund>, IRefundService
    {
        private readonly IOrderService _orderService;

        public RefundService(IRefundRepository refundRepository, IMapper mapper, IOrderService orderService) : base(refundRepository, mapper)
        {
            _orderService = orderService;
        }

        public int AddRefund(RefundVm refundVm)
        {
            if (refundVm is null)
            {
                throw new BusinessException($"{typeof(RefundVm).Name} cannot be null");
            }

            if (refundVm.Id != 0)
            {
                throw new BusinessException("When adding object Id should be equals 0");
            }

            if (refundVm.RefundDate == new DateTime())
            {
                refundVm.RefundDate = DateTime.Now;
            }

            if (refundVm.CustomerId == 0)
            {
                var customerId = _orderService.GetAllOrders().Where(o => o.Id == refundVm.OrderId).Select(or => or.CustomerId).FirstOrDefault();
                if (customerId == 0)
                {
                    throw new BusinessException($"There is no order with id = {refundVm.OrderId}");
                }
                refundVm.CustomerId = customerId;
            }

            var refund = _mapper.Map<Refund>(refundVm);
            var id = _repo.AddRefund(refund);
            _orderService.AddRefundToOrder(refundVm.OrderId, id);
            return id;
        }

        public void DeleteRefund(int id)
        {
            _orderService.DeleteRefundFromOrder(id);
            _repo.DeleteRefund(id);
        }

        public RefundVm GetRefundById(int id)
        {
            var tag = Get(id);
            return tag;
        }

        public RefundDetailsVm GetRefundDetails(int id)
        {
            var refund = _repo.GetAll().Include(oi => oi.OrderItems).ThenInclude(i => i.Item).Where(r => r.Id == id).FirstOrDefault();
            var refundVm = _mapper.Map<RefundDetailsVm>(refund);
            return refundVm;
        }

        public ListForRefundVm GetRefunds(int pageSize, int pageNo, string searchString)
        {
            var refunds = _repo.GetAllRefunds().Where(r => r.Reason.StartsWith(searchString)
                           || r.RefundDate.ToString().StartsWith(searchString))
                           .ProjectTo<RefundVm>(_mapper.ConfigurationProvider)
                           .ToList();
            var refundsToShow = refunds.Skip(pageSize * (pageNo - 1)).Take(pageSize).ToList();

            var refundsList = new ListForRefundVm()
            {
                PageSize = pageSize,
                CurrentPage = pageNo,
                SearchString = searchString,
                Refunds = refundsToShow,
                Count = refunds.Count
            };

            return refundsList;
        }

        public IEnumerable<RefundVm> GetRefunds(Expression<Func<Refund, bool>> expression)
        {
            var refunds = _repo.GetAll().Where(expression)
               .ProjectTo<RefundVm>(_mapper.ConfigurationProvider);
            var refundsToShow = refunds.ToList();

            return refundsToShow;
        }

        public bool RefundExists(int id)
        {
            var refund = _repo.GetById(id);
            var exists = refund != null;

            if (exists)
            {
                _repo.DetachEntity(refund);
            }

            return exists;
        }

        public void UpdateRefund(RefundVm refundVm)
        {
            if (refundVm is null)
            {
                throw new BusinessException($"{typeof(RefundVm).Name} cannot be null");
            }

            var refund = _mapper.Map<Refund>(refundVm);
            if (refund != null)
            {
                _repo.UpdateRefund(refund);
            }
        }

        public bool SameReasonNotExists(string reasonRefund)
        {
            if (string.IsNullOrWhiteSpace(reasonRefund))
            {
                throw new BusinessException("Check your reason if is not null");
            }

            var refunds = _repo.GetAllRefunds();
            var refund = refunds.Where(r => string.Equals(r.Reason, reasonRefund,
                   StringComparison.OrdinalIgnoreCase)).FirstOrDefault();
            if (refund != null)
            {
                return false;
            }
            else
            {
                return true;
            }
        }
    }
}