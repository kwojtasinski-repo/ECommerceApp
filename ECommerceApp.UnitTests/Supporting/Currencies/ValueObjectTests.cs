using ECommerceApp.Domain.Shared;
using ECommerceApp.Domain.Supporting.Currencies;
using ECommerceApp.Domain.Supporting.Currencies.ValueObjects;
using AwesomeAssertions;
using System;
using Xunit;

namespace ECommerceApp.UnitTests.Supporting.Currencies
{
    public class ValueObjectTests
    {
        [Fact]
        public void CurrencyCode_ValidCode_ShouldCreate()
        {
            var code = new CurrencyCode("eur");

            code.Value.Should().Be("EUR");
        }

        [Fact]
        public void CurrencyCode_ThreeLetterUppercase_ShouldCreate()
        {
            var code = new CurrencyCode("PLN");

            code.Value.Should().Be("PLN");
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        public void CurrencyCode_EmptyOrNull_ShouldThrowDomainException(string value)
        {
            Action action = () => new CurrencyCode(value);

            action.Should().ThrowExactly<DomainException>().WithMessage("Currency code is required.");
        }

        [Theory]
        [InlineData("AB")]
        [InlineData("ABCD")]
        [InlineData("A")]
        public void CurrencyCode_InvalidLength_ShouldThrowDomainException(string value)
        {
            Action action = () => new CurrencyCode(value);

            action.Should().ThrowExactly<DomainException>().WithMessage("Currency code must be exactly 3 characters.");
        }

        [Fact]
        public void CurrencyCode_LowercaseInput_ShouldNormalizeToUppercase()
        {
            var code = new CurrencyCode("usd");

            code.Value.Should().Be("USD");
        }

        [Theory]
        [InlineData("AAA")]
        [InlineData("ZZZ")]
        [InlineData("XYZ")]
        public void CurrencyCode_UnknownIso4217Code_ShouldThrowDomainException(string value)
        {
            Action action = () => new CurrencyCode(value);

            action.Should().ThrowExactly<DomainException>()
                .WithMessage($"'{value}' is not a valid ISO 4217 currency code.");
        }

        [Fact]
        public void CurrencyCode_IsKnownIso4217Code_ValidCode_ShouldReturnTrue()
        {
            CurrencyCode.IsKnownIso4217Code("eur").Should().BeTrue();
            CurrencyCode.IsKnownIso4217Code("PLN").Should().BeTrue();
        }

        [Fact]
        public void CurrencyCode_IsKnownIso4217Code_InvalidCode_ShouldReturnFalse()
        {
            CurrencyCode.IsKnownIso4217Code("AAA").Should().BeFalse();
            CurrencyCode.IsKnownIso4217Code(null).Should().BeFalse();
        }

        [Fact]
        public void CurrencyDescription_ValidDescription_ShouldCreate()
        {
            var description = new CurrencyDescription("Polish zloty");

            description.Value.Should().Be("Polish zloty");
        }

        [Fact]
        public void CurrencyDescription_WhitespaceInput_ShouldTrim()
        {
            var description = new CurrencyDescription("  Polish zloty  ");

            description.Value.Should().Be("Polish zloty");
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        public void CurrencyDescription_EmptyOrNull_ShouldThrowDomainException(string value)
        {
            Action action = () => new CurrencyDescription(value);

            action.Should().ThrowExactly<DomainException>().WithMessage("Currency description is required.");
        }

        [Fact]
        public void CurrencyDescription_TooLong_ShouldThrowDomainException()
        {
            var longDescription = new string('a', 301);

            Action action = () => new CurrencyDescription(longDescription);

            action.Should().ThrowExactly<DomainException>().WithMessage("Currency description must not exceed 300 characters.");
        }

        [Fact]
        public void CurrencyId_ShouldHoldValue()
        {
            var id = new CurrencyId(42);

            id.Value.Should().Be(42);
        }

        [Fact]
        public void CurrencyId_ImplicitConversion_ShouldReturnInt()
        {
            var id = new CurrencyId(7);
            int value = id;

            value.Should().Be(7);
        }

        [Fact]
        public void CurrencyRateId_ShouldHoldValue()
        {
            var id = new CurrencyRateId(99);

            id.Value.Should().Be(99);
        }
    }
}
