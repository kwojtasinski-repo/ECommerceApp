﻿using ECommerceApp.Application.Exceptions;
using ECommerceApp.Application.Services.Orders;
using ECommerceApp.Application.Services.Refunds;
using ECommerceApp.Application.ViewModels.Order;
using ECommerceApp.Application.ViewModels.Refund;
using ECommerceApp.Domain.Interface;
using ECommerceApp.UnitTests.Common;
using FluentAssertions;
using Moq;
using System;
using System.Collections.Generic;
using Xunit;

namespace ECommerceApp.UnitTests.Services.Refund
{
    public class RefundServiceTests : BaseTest
    {
        private readonly Mock<IRefundRepository> _refundRepository;
        private readonly Mock<IOrderService> _orderService;

        public RefundServiceTests()
        {
            _refundRepository = new Mock<IRefundRepository>();
            _orderService = new Mock<IOrderService>();
        }

        [Fact]
        public void given_valid_refund_should_add()
        {
            var id = 0;
            var customerId = 1;
            var orderId = 1;
            var refund = CreateRefundVm(id, orderId, customerId);
            var refundService = new RefundService(_refundRepository.Object, _mapper, _orderService.Object);

            refundService.AddRefund(refund);

            _refundRepository.Verify(r => r.AddRefund(It.IsAny<Domain.Model.Refund>()), Times.Once);
            _orderService.Verify(o => o.AddRefundToOrder(It.IsAny<int>(), It.IsAny<int>()), Times.Once);
        }

        [Fact]
        public void given_invalid_refund_should_throw_an_exception()
        {
            var id = 1;
            var customerId = 1;
            var orderId = 1;
            var refund = CreateRefundVm(id, orderId, customerId);
            var refundService = new RefundService(_refundRepository.Object, _mapper, _orderService.Object);

            Action action = () => refundService.AddRefund(refund);

            action.Should().ThrowExactly<BusinessException>().WithMessage("When adding object Id should be equals 0");
        }

        [Fact]
        public void given_refund_with_invalid_customer_id_should_throw_an_exception()
        {
            var id = 0;
            var customerId = 0;
            var orderId = 1;
            var refund = CreateRefundVm(id, customerId, orderId);
            _orderService.Setup(o => o.GetAllOrders()).Returns(new List<OrderForListVm>());
            var refundService = new RefundService(_refundRepository.Object, _mapper, _orderService.Object);

            Action action = () => refundService.AddRefund(refund);

            action.Should().ThrowExactly<BusinessException>().Where(be => be.Message.Contains(orderId.ToString()));
        }

        [Fact]
        public void given_refund_with_invalid_customer_id_should_find_customer_and_add()
        {
            var id = 0;
            var customerId = 0;
            var orderId = 1;
            var refund = CreateRefundVm(id, customerId, orderId);
            var order = CreateOrder(orderId);
            _orderService.Setup(o => o.GetCustomerFromOrder(orderId)).Returns(order.CustomerId);
            var refundService = new RefundService(_refundRepository.Object, _mapper, _orderService.Object);

            refundService.AddRefund(refund);

            _refundRepository.Verify(r => r.AddRefund(It.IsAny<Domain.Model.Refund>()), Times.Once);
            _orderService.Verify(o => o.AddRefundToOrder(It.IsAny<int>(), It.IsAny<int>()), Times.Once);
        }

        [Fact]
        public void given_valid_refund_id_refund_should_exists()
        {
            var id = 1;
            var refund = CreateRefund(id, 1, 1);
            _refundRepository.Setup(r => r.GetById(id)).Returns(refund);
            var refundService = new RefundService(_refundRepository.Object, _mapper, _orderService.Object);

            var exists = refundService.RefundExists(id);

            exists.Should().BeTrue();
        }

        [Fact]
        public void given_invalid_refund_id_refund_shouldnt_exists()
        {
            var id = 1;
            var refundService = new RefundService(_refundRepository.Object, _mapper, _orderService.Object);

            var exists = refundService.RefundExists(id);

            exists.Should().BeFalse();
        }

        [Fact]
        public void given_valid_refund_should_update()
        {
            var refund = CreateRefundVm(1, 1, 1);
            _refundRepository.Setup(r => r.ExistsById(refund.Id)).Returns(true);
            var refundService = new RefundService(_refundRepository.Object, _mapper, _orderService.Object);

            refundService.UpdateRefund(refund);

            _refundRepository.Verify(r => r.UpdateRefund(It.IsAny<Domain.Model.Refund>()), Times.Once);
        }

        [Fact]
        public void given_proper_reason_when_compare_refund_should_return_true()
        {
            string reason = "This is text";
            var refund = CreateRefund(1, 1, 1);
            refund.Reason = reason;
            var refundService = new RefundService(_refundRepository.Object, _mapper, _orderService.Object);

            var sameReasonExists = refundService.SameReasonNotExists(reason);

            sameReasonExists.Should().BeFalse();
        }

        [Fact]
        public void given_proper_reason_when_compare_refund_should_return_false()
        {
            string reason = "abc";
            _refundRepository.Setup(r => r.ExistsByReason(reason)).Returns(true);
            var refundService = new RefundService(_refundRepository.Object, _mapper, _orderService.Object);

            var sameReasonExists = refundService.SameReasonNotExists(reason);

            sameReasonExists.Should().BeTrue();
        }

        [Fact]
        public void given_invalid_reason_when_compare_refund_should_throw_an_exception()
        {
            string reason = "";
            var refundService = new RefundService(_refundRepository.Object, _mapper, _orderService.Object);

            Action action = () => refundService.SameReasonNotExists(reason);

            action.Should().Throw<BusinessException>().WithMessage("Check your reason if is not null");
        }

        [Fact]
        public void given_null_refund_when_add_should_throw_an_exception()
        {
            var refundService = new RefundService(_refundRepository.Object, _mapper, _orderService.Object);

            Action action = () => refundService.AddRefund(null);

            action.Should().ThrowExactly<BusinessException>().Which.Message.Contains("cannot be null");
        }

        [Fact]
        public void given_null_refund_when_update_should_throw_an_exception()
        {
            var refundService = new RefundService(_refundRepository.Object, _mapper, _orderService.Object);

            Action action = () => refundService.UpdateRefund(null);

            action.Should().ThrowExactly<BusinessException>().Which.Message.Contains("cannot be null");
        }

        private static RefundVm CreateRefundVm(int id, int customerId, int orderId)
        {
            var refund = new RefundVm
            {
                Id = id,
                Accepted = true,
                CustomerId = customerId,
                OnWarranty = true,
                OrderId = orderId,
                Reason = "",
                RefundDate = DateTime.Now
            };

            return refund;
        }

        private static Domain.Model.Refund CreateRefund(int id, int customerId, int orderId)
        {
            var refund = new Domain.Model.Refund
            {
                Id = id,
                Accepted = true,
                CustomerId = customerId,
                OnWarranty = true,
                OrderId = orderId,
                Reason = "",
                RefundDate = DateTime.Now
            };

            return refund;
        }

        private static OrderForListVm CreateOrder(int orderId)
        {
            var order = new OrderForListVm
            {
                Id = orderId,
                CustomerId = 1
            };
            return order;
        }
    }
}
