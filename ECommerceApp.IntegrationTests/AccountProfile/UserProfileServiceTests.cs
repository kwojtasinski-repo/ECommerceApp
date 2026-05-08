using ECommerceApp.Application.AccountProfile.DTOs;
using ECommerceApp.Application.AccountProfile.Services;
using ECommerceApp.Application.Exceptions;
using ECommerceApp.Shared.TestInfrastructure;
using Shouldly;
using System.Threading.Tasks;
using Xunit;

namespace ECommerceApp.IntegrationTests.AccountProfile
{
    public class UserProfileServiceTests : BcBaseTest<IUserProfileService>
    {
        public UserProfileServiceTests(ITestOutputHelper output) : base(output) { }

        private const string TestUserId = "user-profile-test-001";

        private CreateUserProfileDto CreateValidDto(string userId = TestUserId) => new(
            UserId: userId,
            FirstName: "Jan",
            LastName: "Kowalski",
            IsCompany: false,
            NIP: null,
            CompanyName: null,
            Email: "jan@example.com",
            PhoneNumber: "123456789");

        // ── CreateAsync ──────────────────────────────────────────────────

        [Fact]
        public async Task CreateAsync_ValidDto_ShouldReturnProfileId()
        {
            var id = await _service.CreateAsync(CreateValidDto());

            id.ShouldBeGreaterThan(0);
        }

        // ── GetDetailsAsync ──────────────────────────────────────────────

        [Fact]
        public async Task GetDetailsAsync_NonExistent_ShouldReturnNull()
        {
            var result = await _service.GetDetailsAsync(int.MaxValue);

            result.ShouldBeNull();
        }

        [Fact]
        public async Task GetDetailsAsync_Existing_ShouldReturnProfile()
        {
            var id = await _service.CreateAsync(CreateValidDto());

            var result = await _service.GetDetailsAsync(id);

            result.ShouldNotBeNull();
        }

        // ── GetDetailsByUserIdAsync ──────────────────────────────────────

        [Fact]
        public async Task GetDetailsByUserIdAsync_NonExistent_ShouldReturnNull()
        {
            var result = await _service.GetDetailsByUserIdAsync("non-existent-user");

            result.ShouldBeNull();
        }

        [Fact]
        public async Task GetDetailsByUserIdAsync_Existing_ShouldReturnProfile()
        {
            await _service.CreateAsync(CreateValidDto());

            var result = await _service.GetDetailsByUserIdAsync(TestUserId);

            result.ShouldNotBeNull();
        }

        // ── GetByUserIdAsync ─────────────────────────────────────────────

        [Fact]
        public async Task GetByUserIdAsync_NonExistent_ShouldReturnNull()
        {
            var result = await _service.GetByUserIdAsync("non-existent-user");

            result.ShouldBeNull();
        }

        // ── UpdatePersonalInfoAsync ──────────────────────────────────────

        [Fact]
        public async Task UpdatePersonalInfoAsync_NonExistent_ShouldThrowBusinessException()
        {
            var dto = new UpdateUserProfileDto(
                Id: int.MaxValue,
                FirstName: "Updated",
                LastName: "Name",
                IsCompany: false,
                NIP: null,
                CompanyName: null);

            await Should.ThrowAsync<BusinessException>(
                () => _service.UpdatePersonalInfoAsync(dto));
        }

        [Fact]
        public async Task UpdatePersonalInfoAsync_Existing_ShouldReturnTrue()
        {
            var id = await _service.CreateAsync(CreateValidDto());

            var result = await _service.UpdatePersonalInfoAsync(new UpdateUserProfileDto(
                Id: id,
                FirstName: "Anna",
                LastName: "Nowak",
                IsCompany: false,
                NIP: null,
                CompanyName: null));

            result.ShouldBeTrue();
        }

        // ── UpdateContactInfoAsync ───────────────────────────────────────

        [Fact]
        public async Task UpdateContactInfoAsync_NonExistent_ShouldThrowBusinessException()
        {
            var dto = new UpdateContactInfoDto(Id: int.MaxValue, Email: "new@test.com", PhoneNumber: "999");

            await Should.ThrowAsync<BusinessException>(
                () => _service.UpdateContactInfoAsync(dto));
        }

        [Fact]
        public async Task UpdateContactInfoAsync_Existing_ShouldReturnTrue()
        {
            var id = await _service.CreateAsync(CreateValidDto());

            var result = await _service.UpdateContactInfoAsync(
                new UpdateContactInfoDto(Id: id, Email: "new@example.com", PhoneNumber: "987654321"));

            result.ShouldBeTrue();
        }

        // ── DeleteAsync ──────────────────────────────────────────────────

        [Fact]
        public async Task DeleteAsync_NonExistent_ShouldReturnFalse()
        {
            var result = await _service.DeleteAsync(int.MaxValue);

            result.ShouldBeFalse();
        }

        [Fact]
        public async Task DeleteAsync_Existing_ShouldReturnTrueAndRemoveProfile()
        {
            var id = await _service.CreateAsync(CreateValidDto());

            var result = await _service.DeleteAsync(id);

            result.ShouldBeTrue();

            var details = await _service.GetDetailsAsync(id);
            details.ShouldBeNull();
        }

        // ── ExistsAsync ──────────────────────────────────────────────────

        [Fact]
        public async Task ExistsAsync_NonExistent_ShouldReturnFalse()
        {
            var result = await _service.ExistsAsync(int.MaxValue, TestUserId);

            result.ShouldBeFalse();
        }

        [Fact]
        public async Task ExistsAsync_Existing_ShouldReturnTrue()
        {
            var id = await _service.CreateAsync(CreateValidDto());

            var result = await _service.ExistsAsync(id, TestUserId);

            result.ShouldBeTrue();
        }

        // ── GetAllAsync ──────────────────────────────────────────────────

        [Fact]
        public async Task GetAllAsync_EmptyDatabase_ShouldReturnEmptyPage()
        {
            var result = await _service.GetAllAsync(pageSize: 10, pageNo: 1, searchString: "");

            result.ShouldNotBeNull();
            result.Profiles.ShouldNotBeNull();
        }

        // ── AddAddressAsync ──────────────────────────────────────────────

        [Fact]
        public async Task AddAddressAsync_ExistingProfile_ShouldReturnTrue()
        {
            var id = await _service.CreateAsync(CreateValidDto());

            var result = await _service.AddAddressAsync(id, TestUserId, new AddAddressDto(
                UserProfileId: id,
                Street: "Główna",
                BuildingNumber: "10",
                FlatNumber: 5,
                ZipCode: "00-001",
                City: "Warszawa",
                Country: "PL"));

            result.ShouldBeTrue();
        }

        // ── Full lifecycle ───────────────────────────────────────────────

        [Fact]
        public async Task FullLifecycle_CreateUpdateDelete_ShouldWorkCorrectly()
        {
            // Create
            var id = await _service.CreateAsync(CreateValidDto());
            id.ShouldBeGreaterThan(0);

            // Verify
            var created = await _service.GetDetailsAsync(id);
            created.ShouldNotBeNull();

            // Update personal info
            await _service.UpdatePersonalInfoAsync(new UpdateUserProfileDto(
                Id: id, FirstName: "Piotr", LastName: "Wiśniewski",
                IsCompany: false, NIP: null, CompanyName: null));

            // Update contact info
            await _service.UpdateContactInfoAsync(
                new UpdateContactInfoDto(Id: id, Email: "piotr@example.com", PhoneNumber: "111222333"));

            // Add address
            await _service.AddAddressAsync(id, TestUserId, new AddAddressDto(
                UserProfileId: id, Street: "Lipowa", BuildingNumber: "5",
                FlatNumber: null, ZipCode: "30-200", City: "Kraków", Country: "PL"));

            // Delete
            var deleted = await _service.DeleteAsync(id);
            deleted.ShouldBeTrue();

            // Verify deleted
            var afterDelete = await _service.GetDetailsAsync(id);
            afterDelete.ShouldBeNull();
        }
    }
}

