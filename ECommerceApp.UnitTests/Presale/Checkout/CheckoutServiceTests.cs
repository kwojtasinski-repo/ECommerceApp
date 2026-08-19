using ECommerceApp.Application.Presale.Checkout;
using ECommerceApp.Application.Presale.Checkout.Contracts;
using ECommerceApp.Application.Presale.Checkout.Results;
using ECommerceApp.Application.Messaging;
using ECommerceApp.Application.Presale.Checkout.Messages;
using ECommerceApp.Application.Presale.Checkout.Services;
using ECommerceApp.Application.Presale.Checkout.ViewModels;
using ECommerceApp.Domain.Presale.Checkout;
using AwesomeAssertions;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Logging;
using Moq;
using System;
using System.Collections.Generic;
using System.Linq;
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
        private readonly Mock<IOutboxWriter> _outboxWriter = new();
        private readonly Mock<ILogger<CheckoutService>> _logger = new();
        private readonly ICheckoutService _sut;

        private static readonly PresaleUserId UserId = new("user-1");

        private static readonly CheckoutCustomer DefaultCustomer = new(
            "Jan", "Kowalski", "jan@test.com", "+48123456789",
            false, null, null,
            "ul. Testowa", "1", null,
            "00-001", "Warszawa", "Poland");

        public CheckoutServiceTests()
        {
            var options = new PresaleOptions
            {
                SoftReservationTtl = TimeSpan.FromMinutes(15),
                SoftReservationGracePeriod = TimeSpan.FromMinutes(1),
                PlaceOrderAcceptanceWindow = TimeSpan.FromSeconds(15)
            };
            var optionsMonitor = Mock.Of<IOptionsMonitor<PresaleOptions>>(m => m.CurrentValue == options);
            _sut = new CheckoutService(
                _softReservationService.Object,
                _orderClient.Object,
                _cartService.Object,
                optionsMonitor,
                _outboxWriter.Object,
                _logger.Object);
        }

        private static int _nextId = 1;

        private static SoftReservation MakeReservation(int productId, int qty, decimal unitPrice)
        {
            var r = SoftReservation.Create(productId, "user-1", qty, unitPrice, DateTime.UtcNow.AddMinutes(15));
            EntityIdSetter.Set(r, new SoftReservationId(_nextId++));
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

        private void SetupReservations(params SoftReservation[] reservations)
            => _softReservationService
                .Setup(s => s.GetAllForUserAsync(UserId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(reservations.ToList());

        private void SetupNoReservations()
            => SetupReservations();

        private void SetupCart(CartVm cart)
            => _cartService.Setup(c => c.GetCartAsync(UserId, It.IsAny<CancellationToken>())).ReturnsAsync(cart);

        private void SetupHoldResult(bool result)
            => _softReservationService
                .Setup(s => s.HoldAsync(It.IsAny<int>(), It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(result);

        private void SetupHoldResult(int productId, bool result)
            => _softReservationService
                .Setup(s => s.HoldAsync(productId, UserId.Value, 1, It.IsAny<CancellationToken>()))
                .ReturnsAsync(result);

        private void SetupCartRemoval(Action<IReadOnlyList<PresaleProductId>> capture)
            => _cartService
                .Setup(c => c.RemoveRangeAsync(UserId, It.IsAny<IReadOnlyList<PresaleProductId>>(), It.IsAny<CancellationToken>()))
                .Callback<PresaleUserId, IReadOnlyList<PresaleProductId>, CancellationToken>((_, ids, _) => capture(ids))
                .Returns(Task.CompletedTask);

        private void SetupOrderClientThrows(Exception exception)
            => _orderClient
                .Setup(o => o.PlaceOrderAsync(
                    It.IsAny<int>(), It.IsAny<int>(), It.IsAny<string>(),
                    It.IsAny<CheckoutCustomer>(), It.IsAny<IReadOnlyList<CheckoutLine>>(),
                    It.IsAny<CancellationToken>()))
                .ThrowsAsync(exception);

        private void SetupOrderClientCapturingLines(Action<IReadOnlyList<CheckoutLine>> capture)
            => _orderClient
                .Setup(o => o.PlaceOrderAsync(
                    It.IsAny<int>(), It.IsAny<int>(), It.IsAny<string>(),
                    It.IsAny<CheckoutCustomer>(), It.IsAny<IReadOnlyList<CheckoutLine>>(),
                    It.IsAny<CancellationToken>()))
                .Callback<int, int, string, CheckoutCustomer, IReadOnlyList<CheckoutLine>, CancellationToken>(
                    (_, _, _, _, lines, _) => capture(lines))
                .ReturnsAsync(OrderPlacementResult.Succeeded(42));

        private void SetupOrderClientCapturingContext(Action<int, int, string> capture)
            => _orderClient
                .Setup(o => o.PlaceOrderAsync(
                    It.IsAny<int>(), It.IsAny<int>(), It.IsAny<string>(),
                    It.IsAny<CheckoutCustomer>(), It.IsAny<IReadOnlyList<CheckoutLine>>(),
                    It.IsAny<CancellationToken>()))
                .Callback<int, int, string, CheckoutCustomer, IReadOnlyList<CheckoutLine>, CancellationToken>(
                    (customerId, currencyId, userId, _, _, _) => capture(customerId, currencyId, userId))
                .ReturnsAsync(OrderPlacementResult.Succeeded(1));

        private void SetupRevertThrows(Exception exception)
            => _softReservationService
                .Setup(s => s.RevertAllForUserAsync(UserId, It.IsAny<CancellationToken>()))
                .ThrowsAsync(exception);

        // �� AC: NoSoftReservations when no active reservations ���������������

        [Fact]
        public async Task PlaceOrderAsync_NoActiveReservations_ReturnsNoSoftReservations()
        {
            // Arrange
            SetupNoReservations();

            // Act
            var result = await _sut.PlaceOrderAsync(UserId, customerId: 1, currencyId: 1, DefaultCustomer, TestContext.Current.CancellationToken);

            // Assert
            result.Should().BeOfType<CheckoutResult.NoSoftReservations>();
        }

        // �� AC: On order failure � reservations left intact ������������������

        [Fact]
        public async Task PlaceOrderAsync_OrderFailed_SoftReservationsNotRemoved()
        {
            // Arrange
            var reservation = MakeReservation(productId: 5, qty: 1, unitPrice: 50m);
            SetupReservations(reservation);
            SetupOrderClientFailure();

            // Act
            await _sut.PlaceOrderAsync(UserId, customerId: 1, currencyId: 1, DefaultCustomer, TestContext.Current.CancellationToken);

            // Assert
            _softReservationService.Verify(
                s => s.RemoveAsync(It.IsAny<int>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
                Times.Never);
        }

        [Fact]
        public async Task PlaceOrderAsync_OrderFailed_CartNotCleared()
        {
            // Arrange
            var reservation = MakeReservation(productId: 5, qty: 1, unitPrice: 50m);
            SetupReservations(reservation);
            SetupOrderClientFailure();

            // Act
            await _sut.PlaceOrderAsync(UserId, customerId: 1, currencyId: 1, DefaultCustomer, TestContext.Current.CancellationToken);

            // Assert
            _cartService.Verify(
                c => c.ClearAsync(It.IsAny<PresaleUserId>(), It.IsAny<CancellationToken>()),
                Times.Never);
        }

        [Fact]
        public async Task PlaceOrderAsync_OrderFailed_ReturnsOrderFailedWithReason()
        {
            // Arrange
            var reservation = MakeReservation(productId: 5, qty: 1, unitPrice: 50m);
            SetupReservations(reservation);
            SetupOrderClientFailure("None of the provided cart items were found.");

            // Act
            var result = await _sut.PlaceOrderAsync(UserId, customerId: 1, currencyId: 1, DefaultCustomer, TestContext.Current.CancellationToken);

            // Assert
            result.Should().BeOfType<CheckoutResult.OrderFailed>()
                .Which.Reason.Should().NotBeNullOrEmpty();
        }

        // �� AC: SoftReservation.UnitPrice flows to IOrderClient ��������������

        [Fact]
        public async Task PlaceOrderAsync_Success_UnitPriceFromReservationNotFromCatalog()
        {
            // Arrange
            const decimal lockedPrice = 99.50m;
            var reservation = MakeReservation(productId: 7, qty: 3, unitPrice: lockedPrice);
            SetupReservations(reservation);

            IReadOnlyList<CheckoutLine> capturedLines = null;
            SetupOrderClientCapturingLines(lines => capturedLines = lines);

            // Act
            await _sut.PlaceOrderAsync(UserId, customerId: 1, currencyId: 1, DefaultCustomer, TestContext.Current.CancellationToken);

            // Assert
            capturedLines.Should().NotBeNull();
            capturedLines!.Should().ContainSingle()
                .Which.UnitPrice.Should().Be(lockedPrice);
        }

        [Fact]
        public async Task PlaceOrderAsync_Success_DtoContainsCorrectCustomerAndCurrency()
        {
            // Arrange
            var reservation = MakeReservation(productId: 3, qty: 1, unitPrice: 10m);
            SetupReservations(reservation);

            int capturedCustomerId = 0, capturedCurrencyId = 0;
            string capturedUserId = null;
            SetupOrderClientCapturingContext((customerId, currencyId, userId) =>
            {
                capturedCustomerId = customerId;
                capturedCurrencyId = currencyId;
                capturedUserId = userId;
            });

            // Act
            await _sut.PlaceOrderAsync(UserId, customerId: 7, currencyId: 3, DefaultCustomer, TestContext.Current.CancellationToken);

            // Assert
            capturedCustomerId.Should().Be(7);
            capturedCurrencyId.Should().Be(3);
            capturedUserId.Should().Be("user-1");
        }

        // �� AC: Success � orderId returned ������������������������������������

        [Fact]
        public async Task PlaceOrderAsync_Success_ReturnsSuccessWithOrderId()
        {
            // Arrange
            var reservation = MakeReservation(productId: 3, qty: 1, unitPrice: 10m);
            SetupReservations(reservation);
            SetupOrderClientSuccess(orderId: 99);

            // Act
            var result = await _sut.PlaceOrderAsync(UserId, customerId: 1, currencyId: 1, DefaultCustomer, TestContext.Current.CancellationToken);

            // Assert
            result.Should().BeOfType<CheckoutResult.Success>()
                .Which.OrderId.Should().Be(99);
        }

        // �� AC: Success � reservations committed (cleanup delegated to OrderPlacedHandler) ���

        [Fact]
        public async Task PlaceOrderAsync_Success_CommitsAllReservations()
        {
            // Arrange
            var r1 = MakeReservation(productId: 1, qty: 1, unitPrice: 10m);
            var r2 = MakeReservation(productId: 2, qty: 2, unitPrice: 20m);
            SetupReservations(r1, r2);
            SetupOrderClientSuccess();

            // Act
            await _sut.PlaceOrderAsync(UserId, customerId: 1, currencyId: 1, DefaultCustomer, TestContext.Current.CancellationToken);

            // Assert
            _softReservationService.Verify(
                s => s.CommitAllForUserAsync(UserId, It.IsAny<CancellationToken>()),
                Times.Once);
        }

        [Fact]
        public async Task PlaceOrderAsync_Success_DoesNotRemoveReservationsOrClearCart()
        {
            // Arrange
            var reservation = MakeReservation(productId: 4, qty: 1, unitPrice: 15m);
            SetupReservations(reservation);
            SetupOrderClientSuccess();

            // Act
            await _sut.PlaceOrderAsync(UserId, customerId: 1, currencyId: 1, DefaultCustomer, TestContext.Current.CancellationToken);

            // Assert
            _softReservationService.Verify(
                s => s.RemoveAsync(It.IsAny<int>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
                Times.Never);
            _cartService.Verify(
                c => c.ClearAsync(It.IsAny<PresaleUserId>(), It.IsAny<CancellationToken>()),
                Times.Never);
        }

        // �� AC: On order failure � reservations reverted to Active ������������

        [Fact]
        public async Task PlaceOrderAsync_OrderFailed_RevertsAllReservations()
        {
            // Arrange
            var reservation = MakeReservation(productId: 5, qty: 1, unitPrice: 50m);
            SetupReservations(reservation);
            SetupOrderClientFailure();

            // Act
            await _sut.PlaceOrderAsync(UserId, customerId: 1, currencyId: 1, DefaultCustomer, TestContext.Current.CancellationToken);

            // Assert
            _softReservationService.Verify(
                s => s.RevertAllForUserAsync(UserId, It.IsAny<CancellationToken>()),
                Times.Once);
        }

        [Fact]
        public async Task PlaceOrderAsync_OrderClientThrows_RevertsReservationsAndPropagatesException()
        {
            // Arrange
            var reservation = MakeReservation(productId: 5, qty: 1, unitPrice: 50m);
            var expected = new InvalidOperationException("order client failed");
            SetupReservations(reservation);
            SetupOrderClientThrows(expected);

            // Act
            var action = () => _sut.PlaceOrderAsync(UserId, 1, 1, DefaultCustomer, TestContext.Current.CancellationToken);

            // Assert
            await action.Should().ThrowAsync<InvalidOperationException>().WithMessage("order client failed");
            _softReservationService.Verify(s => s.RevertAllForUserAsync(UserId, It.IsAny<CancellationToken>()), Times.Once);
            _outboxWriter.Verify(o => o.EnqueueAsync(It.IsAny<IMessage>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task PlaceOrderAsync_OrderClientAndRevertThrow_SchedulesRetryAndPropagatesOriginalException()
        {
            // Arrange
            var reservation = MakeReservation(productId: 5, qty: 1, unitPrice: 50m);
            var expected = new InvalidOperationException("order client failed");
            SetupReservations(reservation);
            SetupRevertThrows(new InvalidOperationException("revert failed"));
            SetupOrderClientThrows(expected);

            // Act
            var action = () => _sut.PlaceOrderAsync(UserId, 1, 1, DefaultCustomer, TestContext.Current.CancellationToken);

            // Assert
            await action.Should().ThrowAsync<InvalidOperationException>().WithMessage("order client failed");
            _outboxWriter.Verify(o => o.EnqueueAsync(
                It.Is<CheckoutReservationRevertRequested>(m => m.UserId == UserId.Value),
                It.IsAny<CancellationToken>()), Times.Once);
        }

        // ?? AC: EmptyCart ??????????????????????????????????????????????????????

        [Fact]
        public async Task InitiateAsync_EmptyCart_ReturnsCartEmpty()
        {
            // Arrange
            SetupCart(null);

            // Act
            var result = await _sut.InitiateAsync(UserId, TestContext.Current.CancellationToken);

            // Assert
            result.Should().BeOfType<InitiateCheckoutResult.CartEmpty>();
        }

        [Fact]
        public async Task InitiateAsync_EmptyCart_DoesNotRemoveFromCart()
        {
            // Arrange
            SetupCart(null);

            // Act
            await _sut.InitiateAsync(UserId, TestContext.Current.CancellationToken);

            // Assert
            _cartService.Verify(
                c => c.RemoveRangeAsync(It.IsAny<PresaleUserId>(), It.IsAny<IReadOnlyList<PresaleProductId>>(), It.IsAny<CancellationToken>()),
                Times.Never);
        }

        // ?? AC: AlreadyInProgress guard ????????????????????????????????????????

        [Fact]
        public async Task InitiateAsync_ActiveReservationExists_ReturnsAlreadyInProgress()
        {
            // Arrange
            SetupCart(new CartVm(UserId.Value, new List<CartLineVm> { new(1, 1, null) }));
            var active = MakeReservation(productId: 1, qty: 1, unitPrice: 10m);
            SetupReservations(active);

            // Act
            var result = await _sut.InitiateAsync(UserId, TestContext.Current.CancellationToken);

            // Assert
            result.Should().BeOfType<InitiateCheckoutResult.AlreadyInProgress>();
        }

        [Fact]
        public async Task InitiateAsync_ActiveReservationExists_DoesNotCallHold()
        {
            // Arrange
            SetupCart(new CartVm(UserId.Value, new List<CartLineVm> { new(1, 1, null) }));
            var active = MakeReservation(productId: 1, qty: 1, unitPrice: 10m);
            SetupReservations(active);

            // Act
            await _sut.InitiateAsync(UserId, TestContext.Current.CancellationToken);

            // Assert
            _softReservationService.Verify(
                s => s.HoldAsync(It.IsAny<int>(), It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>()),
                Times.Never);
        }

        // ?? AC: All unavailable ? NothingReserved, no cart cleanup ?????????????

        [Fact]
        public async Task InitiateAsync_AllProductsUnavailable_ReturnsNothingReserved()
        {
            // Arrange
            SetupCart(new CartVm(UserId.Value, new List<CartLineVm> { new(1, 1, null) }));
            SetupNoReservations();
            SetupHoldResult(false);

            // Act
            var result = await _sut.InitiateAsync(UserId, TestContext.Current.CancellationToken);

            // Assert
            result.Should().BeOfType<InitiateCheckoutResult.NothingReserved>();
        }

        [Fact]
        public async Task InitiateAsync_AllProductsUnavailable_DoesNotRemoveFromCart()
        {
            // Arrange
            SetupCart(new CartVm(UserId.Value, new List<CartLineVm> { new(1, 1, null) }));
            SetupNoReservations();
            SetupHoldResult(false);

            // Act
            await _sut.InitiateAsync(UserId, TestContext.Current.CancellationToken);

            // Assert
            _cartService.Verify(
                c => c.RemoveRangeAsync(It.IsAny<PresaleUserId>(), It.IsAny<IReadOnlyList<PresaleProductId>>(), It.IsAny<CancellationToken>()),
                Times.Never);
        }

        // ?? AC: All reserved ? Completed, reserved products removed from cart ??

        [Fact]
        public async Task InitiateAsync_AllProductsReserved_ReturnsCompleted()
        {
            // Arrange
            SetupCart(new CartVm(UserId.Value, new List<CartLineVm> { new(1, 1, null), new(2, 2, null) }));
            SetupNoReservations();
            SetupHoldResult(true);

            // Act
            var result = await _sut.InitiateAsync(UserId, TestContext.Current.CancellationToken);

            // Assert
            result.Should().BeOfType<InitiateCheckoutResult.Completed>()
                .Which.ReservedCount.Should().Be(2);
        }

        [Fact]
        public async Task InitiateAsync_AllProductsReserved_RemovesAllReservedFromCart()
        {
            // Arrange
            SetupCart(new CartVm(UserId.Value, new List<CartLineVm> { new(1, 1, null), new(2, 2, null) }));
            SetupNoReservations();
            SetupHoldResult(true);

            IReadOnlyList<PresaleProductId> capturedIds = null;
            SetupCartRemoval(ids => capturedIds = ids);

            // Act
            await _sut.InitiateAsync(UserId, TestContext.Current.CancellationToken);

            // Assert
            capturedIds.Should().NotBeNull();
            capturedIds!.Select(p => p.Value).Should().BeEquivalentTo(new[] { 1, 2 });
        }

        // AC: Cart removal on partial reservation is now handled by OrderPlacedHandler after order success.

        [Fact]
        public async Task InitiateAsync_PartialReservation_DoesNotRemoveFromCart()
        {
            // Arrange
            SetupCart(new CartVm(UserId.Value, new List<CartLineVm> { new(10, 1, null), new(20, 1, null) }));
            SetupNoReservations();
            SetupHoldResult(10, true);
            SetupHoldResult(20, false);

            IReadOnlyList<PresaleProductId> capturedIds = null;
            SetupCartRemoval(ids => capturedIds = ids);

            // Act
            await _sut.InitiateAsync(UserId, TestContext.Current.CancellationToken);

            // Assert
            capturedIds.Should().ContainSingle()
                .Which.Value.Should().Be(10);
        }
    }
}
