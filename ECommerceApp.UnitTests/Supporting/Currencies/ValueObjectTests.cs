using ECommerceApp.Domain.Shared;
using ECommerceApp.Domain.Supporting.Currencies;
using ECommerceApp.Domain.Supporting.Currencies.ValueObjects;
using FluentAssertions;
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
