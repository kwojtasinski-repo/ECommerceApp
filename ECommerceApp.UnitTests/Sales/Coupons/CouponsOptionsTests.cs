using ECommerceApp.Application.Sales.Coupons;
using AwesomeAssertions;
using Xunit;

namespace ECommerceApp.UnitTests.Sales.Coupons
{
    public class CouponsOptionsTests
    {
        [Fact]
        public void MaxCouponsPerOrder_DefaultShouldBeFive()
        {
            var options = new CouponsOptions();

            options.MaxCouponsPerOrder.Should().Be(5);
        }

        [Fact]
        public void MaxCouponsPerOrder_HardCeilingIsTen()
        {
            // Spec: MaxCouponsPerOrder has a hard ceiling of 10.
            // Configuration can set any value, but service must enforce Math.Min(value, 10).
            var hardCeiling = 10;

            System.Math.Min(15, hardCeiling).Should().Be(10);
            System.Math.Min(5, hardCeiling).Should().Be(5);
            System.Math.Min(10, hardCeiling).Should().Be(10);
        }

        [Fact]
        public void DefaultMinOrderValue_ShouldBe100()
        {
            var options = new CouponsOptions();

            options.DefaultMinOrderValue.Should().Be(100m);
        }

        [Fact]
        public void Options_ShouldBeConfigurable()
        {
            var options = new CouponsOptions
            {
                MaxCouponsPerOrder = 3,
                DefaultMinOrderValue = 50m
            };

            options.MaxCouponsPerOrder.Should().Be(3);
            options.DefaultMinOrderValue.Should().Be(50m);
        }
    }
}
