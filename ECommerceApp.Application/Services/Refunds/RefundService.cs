using AutoMapper;
using ECommerceApp.Application.Abstracts;
using ECommerceApp.Application.Exceptions;
using ECommerceApp.Application.Services.Orders;
using ECommerceApp.Application.ViewModels.Refund;
using ECommerceApp.Domain.Interface;
using ECommerceApp.Domain.Model;
using System;
using System.Collections.Generic;

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

            if (refundVm.CustomerId == default)
            {
                var customerId = _orderService.GetCustomerFromOrder(refundVm.OrderId);
                if (customerId == default)
                {
                    throw new BusinessException($"There is no customer that is related with order with id = {refundVm.OrderId}", "customerNotRelatedWithOrder", new Dictionary<string, string> { { "id", $"{refundVm.OrderId}" } });
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
            var refund = _repo.GetDetailsById(id);
            var refundVm = _mapper.Map<RefundDetailsVm>(refund);
            return refundVm;
        }

        public ListForRefundVm GetRefunds(int pageSize, int pageNo, string searchString)
        {
            var refunds = _mapper.Map<List<RefundVm>>(_repo.GetAllRefunds(pageSize, pageNo, searchString));

            var refundsList = new ListForRefundVm()
            {
                PageSize = pageSize,
                CurrentPage = pageNo,
                SearchString = searchString,
                Refunds = refunds,
                Count = _repo.GetCountBySearchString(searchString)
            };

            return refundsList;
        }

        public IEnumerable<RefundVm> GetRefunds()
        {
            return _mapper.Map<List<RefundVm>>(_repo.GetAllRefunds());
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

            return _repo.ExistsByReason(reasonRefund);
        }
    }
}
