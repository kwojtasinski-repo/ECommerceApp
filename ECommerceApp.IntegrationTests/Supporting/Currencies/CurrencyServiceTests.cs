using ECommerceApp.Application.Exceptions;
using ECommerceApp.Application.Supporting.Currencies.DTOs;
using ECommerceApp.Application.Supporting.Currencies.Services;
using ECommerceApp.Shared.TestInfrastructure;
using Shouldly;
using System.Threading.Tasks;
using Xunit;

namespace ECommerceApp.IntegrationTests.Supporting.Currencies
{
    public class CurrencyServiceTests : BcBaseTest<ICurrencyService>
    {
        public CurrencyServiceTests(ITestOutputHelper output) : base(output) { }

        // ── AddAsync ─────────────────────────────────────────────────────

        [Fact]
        public async Task AddAsync_ValidCurrency_ShouldReturnId()
        {
            var id = await _service.AddAsync(new CreateCurrencyDto("PLN", "Polish Zloty"));

            id.ShouldBeGreaterThan(0);
        }

        [Fact]
        public async Task AddAsync_NullDto_ShouldThrowBusinessException()
        {
            await Should.ThrowAsync<BusinessException>(
                () => _service.AddAsync(null!));
        }

        [Fact]
        public async Task AddAsync_DuplicateCode_ShouldThrowBusinessException()
        {
            await _service.AddAsync(new CreateCurrencyDto("USD", "US Dollar"));

            await Should.ThrowAsync<BusinessException>(
                () => _service.AddAsync(new CreateCurrencyDto("USD", "Another Dollar")));
        }

        // ── GetByIdAsync ─────────────────────────────────────────────────

        [Fact]
        public async Task GetByIdAsync_ExistingCurrency_ShouldReturnCurrency()
        {
            var id = await _service.AddAsync(new CreateCurrencyDto("EUR", "Euro"));

            var result = await _service.GetByIdAsync(id);

            result.ShouldNotBeNull();
            result.Code.ShouldBe("EUR");
            result.Description.ShouldBe("Euro");
        }

        // ── GetAllAsync ──────────────────────────────────────────────────

        [Fact]
        public async Task GetAllAsync_EmptyDatabase_ShouldReturnEmptyList()
        {
            var result = await _service.GetAllAsync();

            result.ShouldNotBeNull();
            result.ShouldBeEmpty();
        }

        [Fact]
        public async Task GetAllAsync_WithCurrencies_ShouldReturnAll()
        {
            await _service.AddAsync(new CreateCurrencyDto("PLN", "Polish Zloty"));
            await _service.AddAsync(new CreateCurrencyDto("EUR", "Euro"));
            await _service.AddAsync(new CreateCurrencyDto("GBP", "British Pound"));

            var result = await _service.GetAllAsync();

            result.ShouldNotBeNull();
            result.Count.ShouldBe(3);
        }

        // ── UpdateAsync ──────────────────────────────────────────────────

        [Fact]
        public async Task UpdateAsync_NonExistent_ShouldReturnFalse()
        {
            var result = await _service.UpdateAsync(
                new UpdateCurrencyDto(int.MaxValue, "XXX", "Non-existent"));

            result.ShouldBeFalse();
        }

        [Fact]
        public async Task UpdateAsync_ExistingCurrency_ShouldReturnTrue()
        {
            var id = await _service.AddAsync(new CreateCurrencyDto("CHF", "Swiss Franc"));

            var result = await _service.UpdateAsync(
                new UpdateCurrencyDto(id, "CHF", "Updated Swiss Franc"));

            result.ShouldBeTrue();

            var updated = await _service.GetByIdAsync(id);
            updated.Description.ShouldBe("Updated Swiss Franc");
        }

        [Fact]
        public async Task UpdateAsync_NullDto_ShouldThrowBusinessException()
        {
            await Should.ThrowAsync<BusinessException>(
                () => _service.UpdateAsync(null!));
        }

        // ── DeleteAsync ──────────────────────────────────────────────────

        [Fact]
        public async Task DeleteAsync_NonExistent_ShouldReturnFalse()
        {
            var result = await _service.DeleteAsync(int.MaxValue);

            result.ShouldBeFalse();
        }

        [Fact]
        public async Task DeleteAsync_ExistingCurrency_ShouldReturnTrueAndRemoveCurrency()
        {
            var id = await _service.AddAsync(new CreateCurrencyDto("JPY", "Japanese Yen"));

            var result = await _service.DeleteAsync(id);

            result.ShouldBeTrue();

            var deleted = await _service.GetByIdAsync(id);
            deleted.ShouldBeNull();
        }

        // ── GetAllAsync (paged) ──────────────────────────────────────────

        [Fact]
        public async Task GetAllAsync_Paged_ShouldReturnPagedResult()
        {
            await _service.AddAsync(new CreateCurrencyDto("PLN", "Polish Zloty"));
            await _service.AddAsync(new CreateCurrencyDto("EUR", "Euro"));

            var result = await _service.GetAllAsync(pageSize: 10, pageNo: 1, searchString: "");

            result.ShouldNotBeNull();
            result.Currencies.ShouldNotBeNull();
        }

        // ── Full lifecycle ───────────────────────────────────────────────

        [Fact]
        public async Task FullLifecycle_AddUpdateDelete_ShouldWorkCorrectly()
        {
            // Add
            var id = await _service.AddAsync(new CreateCurrencyDto("NOK", "Norwegian Krone"));
            id.ShouldBeGreaterThan(0);

            // Read
            var created = await _service.GetByIdAsync(id);
            created.ShouldNotBeNull();
            created.Code.ShouldBe("NOK");

            // Update
            var updated = await _service.UpdateAsync(new UpdateCurrencyDto(id, "NOK", "Updated Krone"));
            updated.ShouldBeTrue();

            // Verify update
            var afterUpdate = await _service.GetByIdAsync(id);
            afterUpdate!.Description.ShouldBe("Updated Krone");

            // Delete
            var deleted = await _service.DeleteAsync(id);
            deleted.ShouldBeTrue();

            // Verify deleted
            var afterDelete = await _service.GetByIdAsync(id);
            afterDelete.ShouldBeNull();
        }
    }
}

