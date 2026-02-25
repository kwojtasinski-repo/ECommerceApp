using ECommerceApp.Domain.Shared;
using ECommerceApp.Domain.Supporting.Currencies;
using FluentAssertions;
using System;
using Xunit;

namespace ECommerceApp.UnitTests.Supporting.Currencies
{
    public class CurrencyEntityTests
    {
        [Fact]
        public void Create_ValidParameters_ShouldCreateCurrency()
        {
            var currency = Currency.Create("PLN", "Polish zloty");

            currency.Code.Value.Should().Be("PLN");
            currency.Description.Value.Should().Be("Polish zloty");
        }

        [Fact]
        public void Create_LowercaseCode_ShouldNormalizeToUppercase()
        {
            var currency = Currency.Create("eur", "Euro");

            currency.Code.Value.Should().Be("EUR");
        }

        [Fact]
        public void Create_EmptyCode_ShouldThrowDomainException()
        {
            Action action = () => Currency.Create("", "Description");

            action.Should().ThrowExactly<DomainException>();
        }

        [Fact]
        public void Create_EmptyDescription_ShouldThrowDomainException()
        {
            Action action = () => Currency.Create("PLN", "");

            action.Should().ThrowExactly<DomainException>();
        }

        [Fact]
        public void Update_ValidParameters_ShouldUpdateCurrency()
        {
            var currency = Currency.Create("PLN", "Polish zloty");

            currency.Update("EUR", "Euro");

            currency.Code.Value.Should().Be("EUR");
            currency.Description.Value.Should().Be("Euro");
        }

        [Fact]
        public void Update_InvalidCode_ShouldThrowDomainException()
        {
            var currency = Currency.Create("PLN", "Polish zloty");

            Action action = () => currency.Update("", "Euro");

            action.Should().ThrowExactly<DomainException>();
        }

        [Fact]
        public void PlnId_ShouldBeOne()
        {
            Currency.PlnId.Value.Should().Be(1);
        }

        [Fact]
        public void CurrencyRate_Create_ValidParameters_ShouldCreate()
        {
            var currencyId = new CurrencyId(1);
            var date = new DateTime(2024, 1, 15, 14, 30, 0);

            var rate = CurrencyRate.Create(currencyId, 4.5m, date);

            rate.CurrencyId.Should().Be(currencyId);
            rate.Rate.Should().Be(4.5m);
            rate.CurrencyDate.Should().Be(new DateTime(2024, 1, 15));
        }

        [Fact]
        public void CurrencyRate_Create_ZeroRate_ShouldThrowDomainException()
        {
            var currencyId = new CurrencyId(1);

            Action action = () => CurrencyRate.Create(currencyId, 0, DateTime.Now);

            action.Should().ThrowExactly<DomainException>().WithMessage("Rate must be positive.");
        }

        [Fact]
        public void CurrencyRate_Create_NegativeRate_ShouldThrowDomainException()
        {
            var currencyId = new CurrencyId(1);

            Action action = () => CurrencyRate.Create(currencyId, -1m, DateTime.Now);

            action.Should().ThrowExactly<DomainException>().WithMessage("Rate must be positive.");
        }

        [Fact]
        public void CurrencyRate_Create_NullCurrencyId_ShouldThrowDomainException()
        {
            Action action = () => CurrencyRate.Create(null, 1m, DateTime.Now);

            action.Should().ThrowExactly<DomainException>().WithMessage("Currency id is required.");
        }

        [Fact]
        public void CurrencyRate_Create_ShouldTruncateTimeFromDate()
        {
            var currencyId = new CurrencyId(2);
            var dateWithTime = new DateTime(2024, 6, 15, 10, 30, 45);

            var rate = CurrencyRate.Create(currencyId, 4.6m, dateWithTime);

            rate.CurrencyDate.Should().Be(new DateTime(2024, 6, 15));
            rate.CurrencyDate.TimeOfDay.Should().Be(TimeSpan.Zero);
        }
    }
}
