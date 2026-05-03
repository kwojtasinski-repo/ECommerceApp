using ECommerceApp.Domain.Sales.Orders.ValueObjects;
using ECommerceApp.Domain.Shared;
using AwesomeAssertions;
using System.Text.RegularExpressions;
using Xunit;

namespace ECommerceApp.UnitTests.Sales.Orders.ValueObjects
{
    public class OrderNumberTests
    {
        private static readonly Regex ExpectedFormat =
            new(@"^ORD-\d{8}-[A-F0-9]{8}$", RegexOptions.Compiled);

        // ── Parse — valid ─────────────────────────────────────────────────────

        [Fact]
        public void Parse_ValidFormat_ShouldReturnOrderNumber()
        {
            var number = OrderNumber.Parse("ORD-20260310-A1B2C3D4");

            number.Value.Should().Be("ORD-20260310-A1B2C3D4");
        }

        [Fact]
        public void Parse_ValidFormat_ShouldRoundTripViaToString()
        {
            var number = OrderNumber.Parse("ORD-20260310-DEADBEEF");

            number.ToString().Should().Be("ORD-20260310-DEADBEEF");
        }

        // ── Parse — null / whitespace ─────────────────────────────────────────

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        public void Parse_NullOrWhitespace_ShouldThrowDomainException(string value)
        {
            var act = () => OrderNumber.Parse(value);

            act.Should().Throw<DomainException>();
        }

        // ── Parse — invalid format ────────────────────────────────────────────

        [Theory]
        [InlineData("ORD-2026-A1B2C3D4")]          // date too short
        [InlineData("ord-20260310-A1B2C3D4")]       // lowercase prefix
        [InlineData("20260310-A1B2C3D4")]            // missing ORD prefix
        [InlineData("ORD-20260310-a1b2c3d4")]        // lowercase hex segment
        [InlineData("ORD-20260310-A1B2C3D")]         // hex segment too short (7 chars)
        [InlineData("ORD-20260310-A1B2C3D4X")]       // hex segment too long (9 chars)
        [InlineData("ORD-YYYYMMDD-A1B2C3D4")]        // non-digits in date segment
        [InlineData("ORD-20260310A1B2C3D4")]         // missing separator between date and hex
        [InlineData("ORD_20260310_A1B2C3D4")]        // underscores instead of hyphens
        public void Parse_InvalidFormat_ShouldThrowDomainException(string value)
        {
            var act = () => OrderNumber.Parse(value);

            act.Should().Throw<DomainException>()
               .WithMessage("*OrderNumber*");
        }

        // ── Generate ──────────────────────────────────────────────────────────

        [Fact]
        public void Generate_Always_ShouldMatchExpectedFormat()
        {
            var number = OrderNumber.Generate();

            ExpectedFormat.IsMatch(number.Value).Should().BeTrue(
                because: $"'{number.Value}' must match ORD-YYYYMMDD-XXXXXXXX");
        }

        [Fact]
        public void Generate_Always_ShouldStartWithORDPrefix()
        {
            var number = OrderNumber.Generate();

            number.Value.Should().StartWith("ORD-");
        }

        [Fact]
        public void Generate_CalledTwice_ShouldProduceDifferentValues()
        {
            var first = OrderNumber.Generate();
            var second = OrderNumber.Generate();

            first.Value.Should().NotBe(second.Value);
        }

        // ── Implicit string operator ──────────────────────────────────────────

        [Fact]
        public void ImplicitStringConversion_FromOrderNumber_ShouldReturnValue()
        {
            var number = OrderNumber.Parse("ORD-20260310-A1B2C3D4");

            string result = number;

            result.Should().Be("ORD-20260310-A1B2C3D4");
        }

        [Fact]
        public void ImplicitOrderNumberConversion_FromString_ShouldParseValue()
        {
            OrderNumber number = "ORD-20260310-A1B2C3D4";

            number.Value.Should().Be("ORD-20260310-A1B2C3D4");
        }

        [Fact]
        public void ImplicitOrderNumberConversion_FromInvalidString_ShouldThrowDomainException()
        {
            var act = () => { OrderNumber number = "INVALID"; };

            act.Should().Throw<DomainException>();
        }
    }
}
