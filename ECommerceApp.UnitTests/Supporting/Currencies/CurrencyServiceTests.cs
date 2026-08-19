using ECommerceApp.Application.Exceptions;
using ECommerceApp.Application.Supporting.Currencies.DTOs;
using ECommerceApp.Application.Supporting.Currencies.Services;
using ECommerceApp.Domain.Supporting.Currencies;
using ECommerceApp.UnitTests.Common;
using AwesomeAssertions;
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
            _sut = new CurrencyService(_currencyRepository.Object);
        }

        private void SetupCurrencyLookup(Currency currency)
        {
            _currencyRepository.Setup(r => r.GetByIdAsync(It.IsAny<CurrencyId>())).ReturnsAsync(currency);
        }

        private void SetupCurrencyCreation(int currencyId)
        {
            _currencyRepository.Setup(r => r.AddAsync(It.IsAny<Currency>())).ReturnsAsync(new CurrencyId(currencyId));
        }

        private void SetupCurrencyDeletion(bool deleted)
        {
            _currencyRepository.Setup(r => r.DeleteAsync(It.IsAny<CurrencyId>())).ReturnsAsync(deleted);
        }

        private void SetupPaginatedCurrencies(
            int pageSize,
            int pageNumber,
            string searchString,
            List<Currency> currencies,
            int count)
        {
            _currencyRepository.Setup(r => r.GetAllAsync(pageSize, pageNumber, searchString)).ReturnsAsync(currencies);
            _currencyRepository.Setup(r => r.CountBySearchStringAsync(searchString)).ReturnsAsync(count);
        }

        private void SetupAllCurrencies(List<Currency> currencies)
        {
            _currencyRepository.Setup(r => r.GetAllAsync()).ReturnsAsync(currencies);
        }

        [Fact]
        public async Task AddAsync_ValidDto_ShouldAddCurrency()
        {
            // Arrange
            var dto = new CreateCurrencyDto("EUR", "Euro");
            SetupCurrencyCreation(1);

            // Act
            var result = await _sut.AddAsync(dto);

            // Assert
            result.Should().Be(1);
            _currencyRepository.Verify(r => r.AddAsync(It.IsAny<Currency>()), Times.Once);
        }

        [Fact]
        public async Task AddAsync_NullDto_ShouldThrowBusinessException()
        {
            // Arrange
            Func<Task> action = () => _sut.AddAsync(null);

            // Act/Assert
            await action.Should().ThrowExactlyAsync<BusinessException>()
                .WithMessage("*cannot be null*");
        }

        [Fact]
        public async Task UpdateAsync_ValidDto_ShouldUpdateCurrency()
        {
            // Arrange
            var dto = new UpdateCurrencyDto(1, "USD", "US Dollar");
            var currency = Currency.Create("EUR", "Euro");
            SetupCurrencyLookup(currency);

            // Act
            var result = await _sut.UpdateAsync(dto);

            // Assert
            result.Should().BeTrue();
            _currencyRepository.Verify(r => r.UpdateAsync(It.IsAny<Currency>()), Times.Once);
        }

        [Fact]
        public async Task UpdateAsync_NullDto_ShouldThrowBusinessException()
        {
            // Arrange
            Func<Task> action = () => _sut.UpdateAsync(null);

            // Act/Assert
            await action.Should().ThrowExactlyAsync<BusinessException>()
                .WithMessage("*cannot be null*");
        }

        [Fact]
        public async Task UpdateAsync_NonExistentCurrency_ShouldReturnFalse()
        {
            // Arrange
            var dto = new UpdateCurrencyDto(999, "USD", "US Dollar");
            SetupCurrencyLookup(null);

            // Act
            var result = await _sut.UpdateAsync(dto);

            // Assert
            result.Should().BeFalse();
            _currencyRepository.Verify(r => r.UpdateAsync(It.IsAny<Currency>()), Times.Never);
        }

        [Fact]
        public async Task DeleteAsync_ExistingId_ShouldReturnTrue()
        {
            // Arrange
            SetupCurrencyDeletion(true);

            // Act
            var result = await _sut.DeleteAsync(1);

            // Assert
            result.Should().BeTrue();
        }

        [Fact]
        public async Task GetByIdAsync_ExistingId_ShouldReturnCurrencyVm()
        {
            // Arrange
            var currency = Currency.Create("PLN", "Polish zloty");
            SetupCurrencyLookup(currency);

            // Act
            var result = await _sut.GetByIdAsync(1);

            // Assert
            result.Should().NotBeNull();
            result.Code.Should().Be("PLN");
            result.Description.Should().Be("Polish zloty");
        }

        [Fact]
        public async Task GetByIdAsync_NonExistentId_ShouldReturnNull()
        {
            // Arrange
            SetupCurrencyLookup(null);

            // Act
            var result = await _sut.GetByIdAsync(999);

            // Assert
            result.Should().BeNull();
        }

        [Fact]
        public async Task GetAllAsync_ShouldReturnMappedList()
        {
            // Arrange
            var currencies = new List<Currency> { Currency.Create("PLN", "Polish zloty"), Currency.Create("EUR", "Euro") };
            SetupAllCurrencies(currencies);

            // Act
            var result = await _sut.GetAllAsync();

            // Assert
            result.Should().HaveCount(2);
        }

        [Fact]
        public async Task GetAllAsync_Paginated_ShouldReturnCurrencyListVm()
        {
            // Arrange
            var currencies = new List<Currency> { Currency.Create("PLN", "Polish zloty") };
            SetupPaginatedCurrencies(10, 1, "P", currencies, 1);

            // Act
            var result = await _sut.GetAllAsync(10, 1, "P");

            // Assert
            result.Should().NotBeNull();
            result.Currencies.Should().HaveCount(1);
            result.PageSize.Should().Be(10);
            result.CurrentPage.Should().Be(1);
            result.SearchString.Should().Be("P");
            result.Count.Should().Be(1);
        }
    }
}
