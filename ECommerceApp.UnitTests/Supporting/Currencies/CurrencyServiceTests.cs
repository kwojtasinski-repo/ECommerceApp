using ECommerceApp.Application.Exceptions;
using ECommerceApp.Application.Supporting.Currencies.DTOs;
using ECommerceApp.Application.Supporting.Currencies.Services;
using ECommerceApp.Domain.Supporting.Currencies;
using ECommerceApp.UnitTests.Common;
using FluentAssertions;
using Moq;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Xunit;

namespace ECommerceApp.UnitTests.Supporting.Currencies
{
    public class CurrencyServiceTests : BaseTest
    {
        private readonly Mock<ICurrencyRepository> _currencyRepository;
        private readonly CurrencyService _sut;

        public CurrencyServiceTests()
        {
            _currencyRepository = new Mock<ICurrencyRepository>();
            _sut = new CurrencyService(_currencyRepository.Object, _mapper);
        }

        [Fact]
        public async Task AddAsync_ValidDto_ShouldAddCurrency()
        {
            var dto = new CreateCurrencyDto("EUR", "Euro");
            _currencyRepository.Setup(r => r.AddAsync(It.IsAny<Currency>()))
                .ReturnsAsync(new CurrencyId(1));

            var result = await _sut.AddAsync(dto);

            result.Should().Be(1);
            _currencyRepository.Verify(r => r.AddAsync(It.IsAny<Currency>()), Times.Once);
        }

        [Fact]
        public async Task AddAsync_NullDto_ShouldThrowBusinessException()
        {
            Func<Task> action = () => _sut.AddAsync(null);

            await action.Should().ThrowExactlyAsync<BusinessException>()
                .WithMessage("*cannot be null*");
        }

        [Fact]
        public async Task UpdateAsync_ValidDto_ShouldUpdateCurrency()
        {
            var dto = new UpdateCurrencyDto(1, "USD", "US Dollar");
            var currency = Currency.Create("EUR", "Euro");
            _currencyRepository.Setup(r => r.GetByIdAsync(It.IsAny<CurrencyId>())).ReturnsAsync(currency);

            var result = await _sut.UpdateAsync(dto);

            result.Should().BeTrue();
            _currencyRepository.Verify(r => r.UpdateAsync(It.IsAny<Currency>()), Times.Once);
        }

        [Fact]
        public async Task UpdateAsync_NullDto_ShouldThrowBusinessException()
        {
            Func<Task> action = () => _sut.UpdateAsync(null);

            await action.Should().ThrowExactlyAsync<BusinessException>()
                .WithMessage("*cannot be null*");
        }

        [Fact]
        public async Task UpdateAsync_NonExistentCurrency_ShouldReturnFalse()
        {
            var dto = new UpdateCurrencyDto(999, "USD", "US Dollar");
            _currencyRepository.Setup(r => r.GetByIdAsync(It.IsAny<CurrencyId>())).ReturnsAsync((Currency)null);

            var result = await _sut.UpdateAsync(dto);

            result.Should().BeFalse();
            _currencyRepository.Verify(r => r.UpdateAsync(It.IsAny<Currency>()), Times.Never);
        }

        [Fact]
        public async Task DeleteAsync_ExistingId_ShouldReturnTrue()
        {
            _currencyRepository.Setup(r => r.DeleteAsync(It.IsAny<CurrencyId>())).ReturnsAsync(true);

            var result = await _sut.DeleteAsync(1);

            result.Should().BeTrue();
        }

        [Fact]
        public async Task GetByIdAsync_ExistingId_ShouldReturnCurrencyVm()
        {
            var currency = Currency.Create("PLN", "Polish zloty");
            _currencyRepository.Setup(r => r.GetByIdAsync(It.IsAny<CurrencyId>())).ReturnsAsync(currency);

            var result = await _sut.GetByIdAsync(1);

            result.Should().NotBeNull();
            result.Code.Should().Be("PLN");
            result.Description.Should().Be("Polish zloty");
        }

        [Fact]
        public async Task GetByIdAsync_NonExistentId_ShouldReturnNull()
        {
            _currencyRepository.Setup(r => r.GetByIdAsync(It.IsAny<CurrencyId>())).ReturnsAsync((Currency)null);

            var result = await _sut.GetByIdAsync(999);

            result.Should().BeNull();
        }

        [Fact]
        public async Task GetAllAsync_ShouldReturnMappedList()
        {
            var currencies = new List<Currency> { Currency.Create("PLN", "Polish zloty"), Currency.Create("EUR", "Euro") };
            _currencyRepository.Setup(r => r.GetAllAsync()).ReturnsAsync(currencies);

            var result = await _sut.GetAllAsync();

            result.Should().HaveCount(2);
        }

        [Fact]
        public async Task GetAllAsync_Paginated_ShouldReturnCurrencyListVm()
        {
            var currencies = new List<Currency> { Currency.Create("PLN", "Polish zloty") };
            _currencyRepository.Setup(r => r.GetAllAsync(10, 1, "P")).ReturnsAsync(currencies);
            _currencyRepository.Setup(r => r.CountBySearchStringAsync("P")).ReturnsAsync(1);

            var result = await _sut.GetAllAsync(10, 1, "P");

            result.Should().NotBeNull();
            result.Currencies.Should().HaveCount(1);
            result.PageSize.Should().Be(10);
            result.CurrentPage.Should().Be(1);
            result.SearchString.Should().Be("P");
            result.Count.Should().Be(1);
        }
    }
}
