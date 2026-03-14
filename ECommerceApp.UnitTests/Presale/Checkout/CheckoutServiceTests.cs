using ECommerceApp.Application.Presale.Checkout.Contracts;
using ECommerceApp.Application.Presale.Checkout.Results;
using ECommerceApp.Application.Presale.Checkout.Services;
using ECommerceApp.Application.Sales.Orders.DTOs;
using ECommerceApp.Application.Sales.Orders.Results;
using ECommerceApp.Application.Sales.Orders.Services;
using ECommerceApp.Domain.Presale.Checkout;
using FluentAssertions;
using Moq;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace ECommerceApp.UnitTests.Presale.Checkout
{
    public class CheckoutServiceTests
    {
        private readonly Mock<ISoftReservationService> _softReservationService = new();
        private readonly Mock<IStockClient> _stockClient = new();
        private readonly Mock<IOrderService> _orderService = new();
        private readonly Mock<ICartService> _cartService = new();
        private readonly ICheckoutService _sut;

        private static readonly PresaleUserId UserId = new("user-1");

        public CheckoutServiceTests()
        {
            _sut = new CheckoutService(
                _softReservationService.Object,
                _stockClient.Object,
                _orderService.Object,
                _cartService.Object);
        }

        private static int _nextId = 1;

        private static SoftReservation MakeReservation(int productId, int qty, decimal unitPrice)
        {
            var r = SoftReservation.Create(productId, "user-1", qty, unitPrice, DateTime.UtcNow.AddMinutes(15));
            typeof(SoftReservation).GetProperty("Id")!.SetValue(r, new SoftReservationId(_nextId++));
            return r;
        }

        // ── AC: NoSoftReservations when no active reservations ───────────────

        [Fact]
        public async Task PlaceOrderAsync_NoActiveReservations_ReturnsNoSoftReservations()
        {
            _softReservationService
                .Setup(s => s.GetAllForUserAsync(UserId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new List<SoftReservation>());

            var result = await _sut.PlaceOrderAsync(UserId, customerId: 1, currencyId: 1);

            result.Should().BeOfType<CheckoutResult.NoSoftReservations>();
        }

        // ── AC: StockUnavailable when IStockClient.TryReserveAsync returns false ──

        [Fact]
        public async Task PlaceOrderAsync_StockUnavailable_ReturnsStockUnavailableWithProductId()
        {
            var reservation = MakeReservation(productId: 10, qty: 2, unitPrice: 9.99m);
            _softReservationService
                .Setup(s => s.GetAllForUserAsync(UserId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new List<SoftReservation> { reservation });
            _stockClient
                .Setup(s => s.TryReserveAsync(10, 2, It.IsAny<CancellationToken>()))
                .ReturnsAsync(false);

            var result = await _sut.PlaceOrderAsync(UserId, customerId: 1, currencyId: 1);

            result.Should().BeOfType<CheckoutResult.StockUnavailable>()
                .Which.ProductId.Should().Be(10);
        }

        // ── AC: On order failure — reservations left intact ──────────────────

        [Fact]
        public async Task PlaceOrderAsync_OrderFailed_SoftReservationsNotRemoved()
        {
            var reservation = MakeReservation(productId: 5, qty: 1, unitPrice: 50m);
            _softReservationService
                .Setup(s => s.GetAllForUserAsync(UserId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new List<SoftReservation> { reservation });
            _stockClient
                .Setup(s => s.TryReserveAsync(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(true);
            _orderService
                .Setup(o => o.PlaceOrderFromPresaleAsync(It.IsAny<PlaceOrderFromPresaleDto>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(PlaceOrderResult.CartItemsNotFound());

            await _sut.PlaceOrderAsync(UserId, customerId: 1, currencyId: 1);

            _softReservationService.Verify(
                s => s.RemoveAsync(It.IsAny<int>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
                Times.Never);
        }

        [Fact]
        public async Task PlaceOrderAsync_OrderFailed_CartNotCleared()
        {
            var reservation = MakeReservation(productId: 5, qty: 1, unitPrice: 50m);
            _softReservationService
                .Setup(s => s.GetAllForUserAsync(UserId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new List<SoftReservation> { reservation });
            _stockClient
                .Setup(s => s.TryReserveAsync(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(true);
            _orderService
                .Setup(o => o.PlaceOrderFromPresaleAsync(It.IsAny<PlaceOrderFromPresaleDto>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(PlaceOrderResult.CustomerNotFound(1));

            await _sut.PlaceOrderAsync(UserId, customerId: 1, currencyId: 1);

            _cartService.Verify(
                c => c.ClearAsync(It.IsAny<PresaleUserId>(), It.IsAny<CancellationToken>()),
                Times.Never);
        }

        [Fact]
        public async Task PlaceOrderAsync_OrderFailed_ReturnsOrderFailedWithReason()
        {
            var reservation = MakeReservation(productId: 5, qty: 1, unitPrice: 50m);
            _softReservationService
                .Setup(s => s.GetAllForUserAsync(UserId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new List<SoftReservation> { reservation });
            _stockClient
                .Setup(s => s.TryReserveAsync(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(true);
            _orderService
                .Setup(o => o.PlaceOrderFromPresaleAsync(It.IsAny<PlaceOrderFromPresaleDto>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(PlaceOrderResult.CartItemsNotFound());

            var result = await _sut.PlaceOrderAsync(UserId, customerId: 1, currencyId: 1);

            result.Should().BeOfType<CheckoutResult.OrderFailed>()
                .Which.Reason.Should().NotBeNullOrEmpty();
        }

        // ── AC: SoftReservation.UnitPrice flows to PlaceOrderLineDto ─────────

        [Fact]
        public async Task PlaceOrderAsync_Success_UnitPriceFromReservationNotFromCatalog()
        {
            const decimal lockedPrice = 99.50m;
            var reservation = MakeReservation(productId: 7, qty: 3, unitPrice: lockedPrice);
            _softReservationService
                .Setup(s => s.GetAllForUserAsync(UserId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new List<SoftReservation> { reservation });
            _stockClient
                .Setup(s => s.TryReserveAsync(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(true);

            PlaceOrderFromPresaleDto? capturedDto = null;
            _orderService
                .Setup(o => o.PlaceOrderFromPresaleAsync(It.IsAny<PlaceOrderFromPresaleDto>(), It.IsAny<CancellationToken>()))
                .Callback<PlaceOrderFromPresaleDto, CancellationToken>((dto, _) => capturedDto = dto)
                .ReturnsAsync(PlaceOrderResult.Success(42));

            await _sut.PlaceOrderAsync(UserId, customerId: 1, currencyId: 1);

            capturedDto.Should().NotBeNull();
            capturedDto!.Lines.Should().ContainSingle()
                .Which.UnitPrice.Should().Be(lockedPrice);
        }

        [Fact]
        public async Task PlaceOrderAsync_Success_DtoContainsCorrectCustomerAndCurrency()
        {
            var reservation = MakeReservation(productId: 3, qty: 1, unitPrice: 10m);
            _softReservationService
                .Setup(s => s.GetAllForUserAsync(UserId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new List<SoftReservation> { reservation });
            _stockClient
                .Setup(s => s.TryReserveAsync(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(true);

            PlaceOrderFromPresaleDto? capturedDto = null;
            _orderService
                .Setup(o => o.PlaceOrderFromPresaleAsync(It.IsAny<PlaceOrderFromPresaleDto>(), It.IsAny<CancellationToken>()))
                .Callback<PlaceOrderFromPresaleDto, CancellationToken>((dto, _) => capturedDto = dto)
                .ReturnsAsync(PlaceOrderResult.Success(1));

            await _sut.PlaceOrderAsync(UserId, customerId: 7, currencyId: 3);

            capturedDto!.CustomerId.Should().Be(7);
            capturedDto.CurrencyId.Should().Be(3);
            capturedDto.UserId.Should().Be("user-1");
        }

        // ── AC: Success — orderId returned ────────────────────────────────────

        [Fact]
        public async Task PlaceOrderAsync_Success_ReturnsSuccessWithOrderId()
        {
            var reservation = MakeReservation(productId: 3, qty: 1, unitPrice: 10m);
            _softReservationService
                .Setup(s => s.GetAllForUserAsync(UserId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new List<SoftReservation> { reservation });
            _stockClient
                .Setup(s => s.TryReserveAsync(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(true);
            _orderService
                .Setup(o => o.PlaceOrderFromPresaleAsync(It.IsAny<PlaceOrderFromPresaleDto>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(PlaceOrderResult.Success(99));

            var result = await _sut.PlaceOrderAsync(UserId, customerId: 1, currencyId: 1);

            result.Should().BeOfType<CheckoutResult.Success>()
                .Which.OrderId.Should().Be(99);
        }

        // ── AC: Success — each reservation removed ────────────────────────────

        [Fact]
        public async Task PlaceOrderAsync_Success_RemovesEachReservation()
        {
            var r1 = MakeReservation(productId: 1, qty: 1, unitPrice: 10m);
            var r2 = MakeReservation(productId: 2, qty: 2, unitPrice: 20m);
            _softReservationService
                .Setup(s => s.GetAllForUserAsync(UserId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new List<SoftReservation> { r1, r2 });
            _stockClient
                .Setup(s => s.TryReserveAsync(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(true);
            _orderService
                .Setup(o => o.PlaceOrderFromPresaleAsync(It.IsAny<PlaceOrderFromPresaleDto>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(PlaceOrderResult.Success(1));

            await _sut.PlaceOrderAsync(UserId, customerId: 1, currencyId: 1);

            _softReservationService.Verify(s => s.RemoveAsync(1, "user-1", It.IsAny<CancellationToken>()), Times.Once);
            _softReservationService.Verify(s => s.RemoveAsync(2, "user-1", It.IsAny<CancellationToken>()), Times.Once);
        }

        // ── AC: Success — cart cleared ────────────────────────────────────────

        [Fact]
        public async Task PlaceOrderAsync_Success_ClearsCart()
        {
            var reservation = MakeReservation(productId: 4, qty: 1, unitPrice: 15m);
            _softReservationService
                .Setup(s => s.GetAllForUserAsync(UserId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new List<SoftReservation> { reservation });
            _stockClient
                .Setup(s => s.TryReserveAsync(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(true);
            _orderService
                .Setup(o => o.PlaceOrderFromPresaleAsync(It.IsAny<PlaceOrderFromPresaleDto>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(PlaceOrderResult.Success(1));

            await _sut.PlaceOrderAsync(UserId, customerId: 1, currencyId: 1);

            _cartService.Verify(c => c.ClearAsync(UserId, It.IsAny<CancellationToken>()), Times.Once);
        }

        // ── AC: Cart cleared only after reservations are removed ─────────────

        [Fact]
        public async Task PlaceOrderAsync_Success_RemovesReservationsBeforeClearingCart()
        {
            var reservation = MakeReservation(productId: 4, qty: 1, unitPrice: 15m);
            _softReservationService
                .Setup(s => s.GetAllForUserAsync(UserId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new List<SoftReservation> { reservation });
            _stockClient
                .Setup(s => s.TryReserveAsync(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(true);
            _orderService
                .Setup(o => o.PlaceOrderFromPresaleAsync(It.IsAny<PlaceOrderFromPresaleDto>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(PlaceOrderResult.Success(1));

            var removeOrder = 0;
            var clearOrder = 0;
            var callSequence = 0;

            _softReservationService
                .Setup(s => s.RemoveAsync(It.IsAny<int>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .Callback(() => removeOrder = ++callSequence)
                .Returns(Task.CompletedTask);
            _cartService
                .Setup(c => c.ClearAsync(It.IsAny<PresaleUserId>(), It.IsAny<CancellationToken>()))
                .Callback(() => clearOrder = ++callSequence)
                .Returns(Task.CompletedTask);

            await _sut.PlaceOrderAsync(UserId, customerId: 1, currencyId: 1);

            removeOrder.Should().BeLessThan(clearOrder);
        }
    }
}
