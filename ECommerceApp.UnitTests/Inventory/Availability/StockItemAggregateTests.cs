using ECommerceApp.Domain.Inventory.Availability;
using ECommerceApp.Domain.Inventory.Availability.Events;
using ECommerceApp.Domain.Inventory.Availability.ValueObjects;
using ECommerceApp.Domain.Shared;
using AwesomeAssertions;
using Xunit;

namespace ECommerceApp.UnitTests.Inventory.Availability
{
    public class StockItemAggregateTests
    {
        // ── Create ────────────────────────────────────────────────────────────

        [Fact]
        public void Create_ValidParameters_ShouldReturnStockItemAndStockAdjustedEvent()
        {
            var (stock, @event) = StockItem.Create(new StockProductId(1), new StockQuantity(10));

            stock.ProductId.Value.Should().Be(1);
            stock.Quantity.Value.Should().Be(10);
            stock.ReservedQuantity.Value.Should().Be(0);
            stock.AvailableQuantity.Should().Be(10);
            @event.Should().BeOfType<StockAdjusted>();
            @event.ProductId.Should().Be(1);
            @event.NewQuantity.Should().Be(10);
            @event.PreviousQuantity.Should().Be(0);
        }

        [Fact]
        public void Create_ZeroInitialQuantity_ShouldSucceed()
        {
            var (stock, _) = StockItem.Create(new StockProductId(1), new StockQuantity(0));

            stock.Quantity.Value.Should().Be(0);
            stock.AvailableQuantity.Should().Be(0);
        }

        [Fact]
        public void Create_NegativeProductId_ShouldThrowDomainException()
        {
            var act = () => StockItem.Create(new StockProductId(0), new StockQuantity(10));

            act.Should().Throw<DomainException>().WithMessage("*ProductId*positive*");
        }

        [Fact]
        public void Create_NegativeInitialQuantity_ShouldThrowDomainException()
        {
            var act = () => StockItem.Create(new StockProductId(1), new StockQuantity(-1));

            act.Should().Throw<DomainException>().WithMessage("*negative*");
        }

        // ── Reserve ───────────────────────────────────────────────────────────

        [Fact]
        public void Reserve_ValidQuantity_ShouldIncrementReservedQuantityAndReturnEvent()
        {
            var (stock, _) = StockItem.Create(new StockProductId(1), new StockQuantity(10));

            var @event = stock.Reserve(3);

            stock.ReservedQuantity.Value.Should().Be(3);
            stock.AvailableQuantity.Should().Be(7);
            stock.Quantity.Value.Should().Be(10);
            @event.Should().BeOfType<StockReserved>();
            @event.Quantity.Should().Be(3);
        }

        [Fact]
        public void Reserve_ExactAvailableQuantity_ShouldSucceed()
        {
            var (stock, _) = StockItem.Create(new StockProductId(1), new StockQuantity(5));

            var @event = stock.Reserve(5);

            stock.ReservedQuantity.Value.Should().Be(5);
            stock.AvailableQuantity.Should().Be(0);
            @event.Quantity.Should().Be(5);
        }

        [Fact]
        public void Reserve_ZeroQuantity_ShouldThrowDomainException()
        {
            var (stock, _) = StockItem.Create(new StockProductId(1), new StockQuantity(10));

            var act = () => stock.Reserve(0);

            act.Should().Throw<DomainException>().WithMessage("*positive*");
        }

        [Fact]
        public void Reserve_MoreThanAvailable_ShouldThrowDomainException()
        {
            var (stock, _) = StockItem.Create(new StockProductId(1), new StockQuantity(5));

            var act = () => stock.Reserve(6);

            act.Should().Throw<DomainException>().WithMessage("*Cannot reserve*");
        }

        [Fact]
        public void Reserve_WhenNoneAvailable_ShouldThrowDomainException()
        {
            var (stock, _) = StockItem.Create(new StockProductId(1), new StockQuantity(3));
            stock.Reserve(3);

            var act = () => stock.Reserve(1);

            act.Should().Throw<DomainException>().WithMessage("*Cannot reserve*");
        }

        // ── Release ───────────────────────────────────────────────────────────

        [Fact]
        public void Release_ValidQuantity_ShouldDecrementReservedQuantityAndReturnEvent()
        {
            var (stock, _) = StockItem.Create(new StockProductId(1), new StockQuantity(10));
            stock.Reserve(5);

            var @event = stock.Release(3);

            stock.ReservedQuantity.Value.Should().Be(2);
            stock.AvailableQuantity.Should().Be(8);
            @event.Should().BeOfType<StockReleased>();
            @event.Quantity.Should().Be(3);
        }

        [Fact]
        public void Release_ExactReservedQuantity_ShouldSucceed()
        {
            var (stock, _) = StockItem.Create(new StockProductId(1), new StockQuantity(10));
            stock.Reserve(4);

            var @event = stock.Release(4);

            stock.ReservedQuantity.Value.Should().Be(0);
            @event.Quantity.Should().Be(4);
        }

        [Fact]
        public void Release_ZeroQuantity_ShouldThrowDomainException()
        {
            var (stock, _) = StockItem.Create(new StockProductId(1), new StockQuantity(10));
            stock.Reserve(5);

            var act = () => stock.Release(0);

            act.Should().Throw<DomainException>().WithMessage("*positive*");
        }

        [Fact]
        public void Release_MoreThanReserved_ShouldThrowDomainException()
        {
            var (stock, _) = StockItem.Create(new StockProductId(1), new StockQuantity(10));
            stock.Reserve(2);

            var act = () => stock.Release(3);

            act.Should().Throw<DomainException>().WithMessage("*Cannot release*");
        }

        // ── Fulfill ───────────────────────────────────────────────────────────

        [Fact]
        public void Fulfill_ValidQuantity_ShouldDecrementBothCountersAndReturnEvent()
        {
            var (stock, _) = StockItem.Create(new StockProductId(1), new StockQuantity(10));
            stock.Reserve(5);

            var @event = stock.Fulfill(5);

            stock.Quantity.Value.Should().Be(5);
            stock.ReservedQuantity.Value.Should().Be(0);
            stock.AvailableQuantity.Should().Be(5);
            @event.Should().BeOfType<StockFulfilled>();
            @event.Quantity.Should().Be(5);
        }

        [Fact]
        public void Fulfill_PartialReservation_ShouldDecrementCorrectly()
        {
            var (stock, _) = StockItem.Create(new StockProductId(1), new StockQuantity(10));
            stock.Reserve(6);

            stock.Fulfill(4);

            stock.Quantity.Value.Should().Be(6);
            stock.ReservedQuantity.Value.Should().Be(2);
            stock.AvailableQuantity.Should().Be(4);
        }

        [Fact]
        public void Fulfill_ZeroQuantity_ShouldThrowDomainException()
        {
            var (stock, _) = StockItem.Create(new StockProductId(1), new StockQuantity(10));
            stock.Reserve(5);

            var act = () => stock.Fulfill(0);

            act.Should().Throw<DomainException>().WithMessage("*positive*");
        }

        [Fact]
        public void Fulfill_MoreThanReserved_ShouldThrowDomainException()
        {
            var (stock, _) = StockItem.Create(new StockProductId(1), new StockQuantity(10));
            stock.Reserve(3);

            var act = () => stock.Fulfill(4);

            act.Should().Throw<DomainException>().WithMessage("*Cannot fulfill*");
        }

        // ── Return ────────────────────────────────────────────────────────────

        [Fact]
        public void Return_ValidQuantity_ShouldIncrementQuantityAndReturnEvent()
        {
            var (stock, _) = StockItem.Create(new StockProductId(1), new StockQuantity(10));
            stock.Reserve(5);
            stock.Fulfill(5);

            var @event = stock.Return(3);

            stock.Quantity.Value.Should().Be(8);
            stock.ReservedQuantity.Value.Should().Be(0);
            @event.Should().BeOfType<StockReturned>();
            @event.Quantity.Should().Be(3);
        }

        [Fact]
        public void Return_ZeroQuantity_ShouldThrowDomainException()
        {
            var (stock, _) = StockItem.Create(new StockProductId(1), new StockQuantity(10));

            var act = () => stock.Return(0);

            act.Should().Throw<DomainException>().WithMessage("*positive*");
        }

        // ── Adjust ────────────────────────────────────────────────────────────

        [Fact]
        public void Adjust_ValidNewQuantity_ShouldSetQuantityAndReturnEvent()
        {
            var (stock, _) = StockItem.Create(new StockProductId(1), new StockQuantity(10));

            var @event = stock.Adjust(new StockQuantity(20));

            stock.Quantity.Value.Should().Be(20);
            @event.Should().BeOfType<StockAdjusted>();
            @event.PreviousQuantity.Should().Be(10);
            @event.NewQuantity.Should().Be(20);
        }

        [Fact]
        public void Adjust_ToZero_ShouldSucceed()
        {
            var (stock, _) = StockItem.Create(new StockProductId(1), new StockQuantity(10));

            var @event = stock.Adjust(new StockQuantity(0));

            stock.Quantity.Value.Should().Be(0);
            @event.NewQuantity.Should().Be(0);
        }

        [Fact]
        public void Adjust_NegativeQuantity_ShouldThrowDomainException()
        {
            var (stock, _) = StockItem.Create(new StockProductId(1), new StockQuantity(10));

            var act = () => stock.Adjust(new StockQuantity(-1));

            act.Should().Throw<DomainException>().WithMessage("*negative*");
        }

        [Fact]
        public void Adjust_BelowReservedQuantity_ShouldThrowDomainException()
        {
            var (stock, _) = StockItem.Create(new StockProductId(1), new StockQuantity(10));
            stock.Reserve(5);

            var act = () => stock.Adjust(new StockQuantity(4));

            act.Should().Throw<DomainException>().WithMessage("*Cannot adjust*reserved*");
        }

        [Fact]
        public void Adjust_EqualToReservedQuantity_ShouldSucceed()
        {
            var (stock, _) = StockItem.Create(new StockProductId(1), new StockQuantity(10));
            stock.Reserve(5);

            var @event = stock.Adjust(new StockQuantity(5));

            stock.Quantity.Value.Should().Be(5);
            stock.AvailableQuantity.Should().Be(0);
            @event.NewQuantity.Should().Be(5);
        }

        // ── AvailableQuantity ─────────────────────────────────────────────────

        [Fact]
        public void AvailableQuantity_WithReservations_ShouldBeQuantityMinusReserved()
        {
            var (stock, _) = StockItem.Create(new StockProductId(1), new StockQuantity(10));
            stock.Reserve(3);
            stock.Reserve(2);

            stock.AvailableQuantity.Should().Be(5);
        }
    }
}
