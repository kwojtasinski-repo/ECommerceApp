using ECommerceApp.Application.Backoffice.Services;
using ECommerceApp.Application.Backoffice.ViewModels;
using ECommerceApp.Application.Supporting.Currencies.Services;
using ECommerceApp.Application.Supporting.Currencies.ViewModels;
using AwesomeAssertions;
using Moq;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace ECommerceApp.UnitTests.Backoffice
{
    public class BackofficeCurrencyServiceTests
    {
        private readonly Mock<ICurrencyService> _currencyService;

        public BackofficeCurrencyServiceTests()
        {
            _currencyService = new Mock<ICurrencyService>();
        }

        private IBackofficeCurrencyService CreateSut() => new BackofficeCurrencyService(_currencyService.Object);

        // ── GetCurrenciesAsync ────────────────────────────────────────────────

        [Fact]
        public async Task GetCurrenciesAsync_WithResults_ReturnsMappedVm()
        {
            // Arrange
            _currencyService
                .Setup(s => s.GetAllAsync())
                .ReturnsAsync(new List<CurrencyVm>
                {
                    new() { Id = 1, Code = "PLN", Description = "Polish Zloty" },
                    new() { Id = 2, Code = "EUR", Description = "Euro" }
                });

            // Act
            var result = await CreateSut().GetCurrenciesAsync(TestContext.Current.CancellationToken);

            // Assert
            result.Currencies.Should().HaveCount(2);
            result.Currencies[0].Id.Should().Be(1);
            result.Currencies[0].Code.Should().Be("PLN");
            result.Currencies[0].Description.Should().Be("Polish Zloty");
            result.Currencies[1].Code.Should().Be("EUR");
        }

        [Fact]
        public async Task GetCurrenciesAsync_EmptyList_ReturnsEmptyVm()
        {
            // Arrange
            _currencyService
                .Setup(s => s.GetAllAsync())
                .ReturnsAsync(new List<CurrencyVm>());

            // Act
            var result = await CreateSut().GetCurrenciesAsync(TestContext.Current.CancellationToken);

            // Assert
            result.Currencies.Should().BeEmpty();
        }

        // ── GetCurrencyDetailAsync ────────────────────────────────────────────

        [Fact]
        public async Task GetCurrencyDetailAsync_ExistingCurrency_ReturnsMappedVm()
        {
            // Arrange
            _currencyService
                .Setup(s => s.GetByIdAsync(1))
                .ReturnsAsync(new CurrencyVm { Id = 1, Code = "PLN", Description = "Polish Zloty" });

            // Act
            var result = await CreateSut().GetCurrencyDetailAsync(1, TestContext.Current.CancellationToken);

            // Assert
            result.Should().NotBeNull();
            result!.Id.Should().Be(1);
            result.Code.Should().Be("PLN");
            result.Description.Should().Be("Polish Zloty");
            result.Rates.Should().BeEmpty();
        }

        [Fact]
        public async Task GetCurrencyDetailAsync_NotFound_ReturnsNull()
        {
            // Arrange
            _currencyService
                .Setup(s => s.GetByIdAsync(99))
                .ReturnsAsync((CurrencyVm)null);

            // Act
            var result = await CreateSut().GetCurrencyDetailAsync(99, TestContext.Current.CancellationToken);

            // Assert
            result.Should().BeNull();
        }
    }
}
