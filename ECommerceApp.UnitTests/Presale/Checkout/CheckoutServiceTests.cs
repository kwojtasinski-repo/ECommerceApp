using ECommerceApp.Application.Presale.Checkout.Contracts;
using ECommerceApp.Application.Presale.Checkout.Results;
using ECommerceApp.Application.Presale.Checkout.Services;
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
        private readonly Mock<IOrderClient> _orderClient = new();
        private readonly Mock<ICartService> _cartService = new();
        private readonly ICheckoutService _sut;

        private static readonly PresaleUserId UserId = new("user-1");

        private static readonly CheckoutCustomer DefaultCustomer = new(
            "Jan", "Kowalski", "jan@test.com", "+48123456789",
            false, null, null,
            "ul. Testowa", "1", null,
            "00-001", "Warszawa", "Poland");

        public CheckoutServiceTests()
        {
            _sut = new CheckoutService(
                _softReservationService.Object,
                _orderClient.Object,
                _cartService.Object);
        }

        private static int _nextId = 1;

        private static SoftReservation MakeReservation(int productId, int qty, decimal unitPrice)
        {
            var r = SoftReservation.Create(productId, "user-1", qty, unitPrice, DateTime.UtcNow.AddMinutes(15));
            typeof(SoftReservation).GetProperty("Id")!.SetValue(r, new SoftReservationId(_nextId++));
            return r;
        }

        private void SetupOrderClientSuccess(int orderId = 42)
            => _orderClient
                .Setup(o => o.PlaceOrderAsync(
                    It.IsAny<int>(), It.IsAny<int>(), It.IsAny<string>(),
                    It.IsAny<CheckoutCustomer>(), It.IsAny<IReadOnlyList<CheckoutLine>>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(OrderPlacementResult.Succeeded(orderId));

        private void SetupOrderClientFailure(string reason = "Customer not found.")
            => _orderClient
                .Setup(o => o.PlaceOrderAsync(
                    It.IsAny<int>(), It.IsAny<int>(), It.IsAny<string>(),
                    It.IsAny<CheckoutCustomer>(), It.IsAny<IReadOnlyList<CheckoutLine>>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(OrderPlacementResult.Failed(reason));

        // ¦¦ AC: NoSoftReservations when no active reservations ¦¦¦¦¦¦¦¦¦¦¦¦¦¦¦

        [Fact]
        public async Task PlaceOrderAsync_NoActiveReservations_ReturnsNoSoftReservations()
        {
            _softReservationService
                .Setup(s => s.GetAllForUserAsync(UserId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new List<SoftReservation>());

            var result = await _sut.PlaceOrderAsync(UserId, customerId: 1, currencyId: 1, DefaultCustomer);

            result.Should().BeOfType<CheckoutResult.NoSoftReservations>();
        }

        // ¦¦ AC: On order failure — reservations left intact ¦¦¦¦¦¦¦¦¦¦¦¦¦¦¦¦¦¦

        [Fact]
        public async Task PlaceOrderAsync_OrderFailed_SoftReservationsNotRemoved()
        {
            var reservation = MakeReservation(productId: 5, qty: 1, unitPrice: 50m);
            _softReservationService
                .Setup(s => s.GetAllForUserAsync(UserId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new List<SoftReservation> { reservation });
            SetupOrderClientFailure();

            await _sut.PlaceOrderAsync(UserId, customerId: 1, currencyId: 1, DefaultCustomer);

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
            SetupOrderClientFailure();

            await _sut.PlaceOrderAsync(UserId, customerId: 1, currencyId: 1, DefaultCustomer);

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
            SetupOrderClientFailure("None of the provided cart items were found.");

            var result = await _sut.PlaceOrderAsync(UserId, customerId: 1, currencyId: 1, DefaultCustomer);

            result.Should().BeOfType<CheckoutResult.OrderFailed>()
                .Which.Reason.Should().NotBeNullOrEmpty();
        }

        // ¦¦ AC: SoftReservation.UnitPrice flows to IOrderClient ¦¦¦¦¦¦¦¦¦¦¦¦¦¦

        [Fact]
        public async Task PlaceOrderAsync_Success_UnitPriceFromReservationNotFromCatalog()
        {
            const decimal lockedPrice = 99.50m;
            var reservation = MakeReservation(productId: 7, qty: 3, unitPrice: lockedPrice);
            _softReservationService
                .Setup(s => s.GetAllForUserAsync(UserId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new List<SoftReservation> { reservation });

            IReadOnlyList<CheckoutLine>? capturedLines = null;
            _orderClient
                .Setup(o => o.PlaceOrderAsync(
                    It.IsAny<int>(), It.IsAny<int>(), It.IsAny<string>(),
                    It.IsAny<CheckoutCustomer>(), It.IsAny<IReadOnlyList<CheckoutLine>>(),
                    It.IsAny<CancellationToken>()))
                .Callback<int, int, string, CheckoutCustomer, IReadOnlyList<CheckoutLine>, CancellationToken>(
                    (_, _, _, _, lines, _) => capturedLines = lines)
                .ReturnsAsync(OrderPlacementResult.Succeeded(42));

            await _sut.PlaceOrderAsync(UserId, customerId: 1, currencyId: 1, DefaultCustomer);

            capturedLines.Should().NotBeNull();
            capturedLines!.Should().ContainSingle()
                .Which.UnitPrice.Should().Be(lockedPrice);
        }

        [Fact]
        public async Task PlaceOrderAsync_Success_DtoContainsCorrectCustomerAndCurrency()
        {
            var reservation = MakeReservation(productId: 3, qty: 1, unitPrice: 10m);
            _softReservationService
                .Setup(s => s.GetAllForUserAsync(UserId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new List<SoftReservation> { reservation });

            int capturedCustomerId = 0, capturedCurrencyId = 0;
            string? capturedUserId = null;
            _orderClient
                .Setup(o => o.PlaceOrderAsync(
                    It.IsAny<int>(), It.IsAny<int>(), It.IsAny<string>(),
                    It.IsAny<CheckoutCustomer>(), It.IsAny<IReadOnlyList<CheckoutLine>>(),
                    It.IsAny<CancellationToken>()))
                .Callback<int, int, string, CheckoutCustomer, IReadOnlyList<CheckoutLine>, CancellationToken>(
                    (cId, currId, uId, _, _, _) =>
                    {
                        capturedCustomerId = cId;
                        capturedCurrencyId = currId;
                        capturedUserId = uId;
                    })
                .ReturnsAsync(OrderPlacementResult.Succeeded(1));

            await _sut.PlaceOrderAsync(UserId, customerId: 7, currencyId: 3, DefaultCustomer);

            capturedCustomerId.Should().Be(7);
            capturedCurrencyId.Should().Be(3);
            capturedUserId.Should().Be("user-1");
        }

        // ¦¦ AC: Success — orderId returned ¦¦¦¦¦¦¦¦¦¦¦¦¦¦¦¦¦¦¦¦¦¦¦¦¦¦¦¦¦¦¦¦¦¦¦¦

        [Fact]
        public async Task PlaceOrderAsync_Success_ReturnsSuccessWithOrderId()
        {
            var reservation = MakeReservation(productId: 3, qty: 1, unitPrice: 10m);
            _softReservationService
                .Setup(s => s.GetAllForUserAsync(UserId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new List<SoftReservation> { reservation });
            SetupOrderClientSuccess(orderId: 99);

            var result = await _sut.PlaceOrderAsync(UserId, customerId: 1, currencyId: 1, DefaultCustomer);

            result.Should().BeOfType<CheckoutResult.Success>()
                .Which.OrderId.Should().Be(99);
        }

        // ¦¦ AC: Success — reservations committed (cleanup delegated to OrderPlacedHandler) ¦¦¦

        [Fact]
        public async Task PlaceOrderAsync_Success_CommitsAllReservations()
        {
            var r1 = MakeReservation(productId: 1, qty: 1, unitPrice: 10m);
            var r2 = MakeReservation(productId: 2, qty: 2, unitPrice: 20m);
            _softReservationService
                .Setup(s => s.GetAllForUserAsync(UserId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new List<SoftReservation> { r1, r2 });
            SetupOrderClientSuccess();

            await _sut.PlaceOrderAsync(UserId, customerId: 1, currencyId: 1, DefaultCustomer);

            _softReservationService.Verify(
                s => s.CommitAllForUserAsync(UserId, It.IsAny<CancellationToken>()),
                Times.Once);
        }

        [Fact]
        public async Task PlaceOrderAsync_Success_DoesNotRemoveReservationsOrClearCart()
        {
            var reservation = MakeReservation(productId: 4, qty: 1, unitPrice: 15m);
            _softReservationService
                .Setup(s => s.GetAllForUserAsync(UserId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new List<SoftReservation> { reservation });
            SetupOrderClientSuccess();

            await _sut.PlaceOrderAsync(UserId, customerId: 1, currencyId: 1, DefaultCustomer);

            _softReservationService.Verify(
                s => s.RemoveAsync(It.IsAny<int>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
                Times.Never);
            _cartService.Verify(
                c => c.ClearAsync(It.IsAny<PresaleUserId>(), It.IsAny<CancellationToken>()),
                Times.Never);
        }

        // ¦¦ AC: On order failure — reservations reverted to Active ¦¦¦¦¦¦¦¦¦¦¦¦

        [Fact]
        public async Task PlaceOrderAsync_OrderFailed_RevertsAllReservations()
        {
            var reservation = MakeReservation(productId: 5, qty: 1, unitPrice: 50m);
            _softReservationService
                .Setup(s => s.GetAllForUserAsync(UserId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new List<SoftReservation> { reservation });
            SetupOrderClientFailure();

            await _sut.PlaceOrderAsync(UserId, customerId: 1, currencyId: 1, DefaultCustomer);

            _softReservationService.Verify(
                s => s.RevertAllForUserAsync(UserId, It.IsAny<CancellationToken>()),
                Times.Once);
        }
    }
}
