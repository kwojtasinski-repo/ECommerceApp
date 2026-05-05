using ECommerceApp.Application.AccountProfile.DTOs;
using ECommerceApp.Application.AccountProfile.Services;
using ECommerceApp.Application.Exceptions;
using ECommerceApp.Domain.AccountProfile;
using AwesomeAssertions;
using Moq;
using System.Threading.Tasks;
using Xunit;

namespace ECommerceApp.UnitTests.AccountProfile
{
    public class UserProfileServiceTests
    {
        private readonly Mock<IUserProfileRepository> _repository;

        public UserProfileServiceTests()
        {
            _repository = new Mock<IUserProfileRepository>();
        }

        private UserProfileService CreateService() => new(_repository.Object);

        private static UserProfile CreateProfile()
        {
            var profile = UserProfile.Create("user-1", "Jan", "Kowalski", false, null, null, "jan@test.com", "123456789");
            return profile;
        }

        [Fact]
        public async Task CreateAsync_ValidDto_ShouldAddProfile()
        {
            var dto = new CreateUserProfileDto("user-1", "Jan", "Kowalski", false, null, null, "jan@test.com", "123456789");
            _repository.Setup(r => r.AddAsync(It.IsAny<UserProfile>())).ReturnsAsync(new UserProfileId(1));

            var result = await CreateService().CreateAsync(dto);

            result.Should().Be(1);
            _repository.Verify(r => r.AddAsync(It.IsAny<UserProfile>()), Times.Once);
        }

        [Fact]
        public async Task UpdatePersonalInfoAsync_ProfileNotFound_ShouldThrowBusinessException()
        {
            var dto = new UpdateUserProfileDto(99, "Jan", "Kowalski", false, null, null);
            _repository.Setup(r => r.GetByIdAsync(new UserProfileId(99), It.IsAny<bool>())).ReturnsAsync((UserProfile)null);

            var act = async () => await CreateService().UpdatePersonalInfoAsync(dto);

            await act.Should().ThrowAsync<BusinessException>();
        }

        [Fact]
        public async Task UpdatePersonalInfoAsync_ValidDto_ShouldUpdateProfile()
        {
            var profile = CreateProfile();
            var dto = new UpdateUserProfileDto(1, "Anna", "Nowak", false, null, null);
            _repository.Setup(r => r.GetByIdAsync(new UserProfileId(1), true)).ReturnsAsync(profile);

            var result = await CreateService().UpdatePersonalInfoAsync(dto);

            result.Should().BeTrue();
            _repository.Verify(r => r.UpdateAsync(It.IsAny<UserProfile>()), Times.Once);
        }

        [Fact]
        public async Task UpdateContactInfoAsync_ProfileNotFound_ShouldThrowBusinessException()
        {
            var dto = new UpdateContactInfoDto(99, "new@test.com", "999");
            _repository.Setup(r => r.GetByIdAsync(new UserProfileId(99), It.IsAny<bool>())).ReturnsAsync((UserProfile)null);

            var act = async () => await CreateService().UpdateContactInfoAsync(dto);

            await act.Should().ThrowAsync<BusinessException>();
        }

        [Fact]
        public async Task AddAddressAsync_ProfileNotFound_ShouldThrowBusinessException()
        {
            var dto = new AddAddressDto(1, "Testowa", "5", null, "12-345", "Warszawa", "PL");
            _repository.Setup(r => r.GetByIdAndUserIdAsync(new UserProfileId(1), "user-1", It.IsAny<bool>())).ReturnsAsync((UserProfile)null);

            var act = async () => await CreateService().AddAddressAsync(1, "user-1", dto);

            await act.Should().ThrowAsync<BusinessException>();
        }

        [Fact]
        public async Task AddAddressAsync_ValidInput_ShouldAddAddressAndSave()
        {
            var profile = CreateProfile();
            var dto = new AddAddressDto(1, "Testowa", "5", null, "12-345", "Warszawa", "PL");
            _repository.Setup(r => r.GetByIdAndUserIdAsync(new UserProfileId(1), "user-1", true)).ReturnsAsync(profile);

            var result = await CreateService().AddAddressAsync(1, "user-1", dto);

            result.Should().BeTrue();
            _repository.Verify(r => r.UpdateAsync(profile), Times.Once);
        }

        [Fact]
        public async Task RemoveAddressAsync_NonExistentAddress_ShouldThrowBusinessException()
        {
            var profile = CreateProfile();
            _repository.Setup(r => r.GetByIdAndUserIdAsync(new UserProfileId(1), "user-1", true)).ReturnsAsync(profile);

            var act = async () => await CreateService().RemoveAddressAsync(1, 99, "user-1");

            await act.Should().ThrowAsync<BusinessException>();
        }

        [Fact]
        public async Task RemoveAddressAsync_LastAddress_ShouldThrowBusinessException()
        {
            var profile = CreateProfile();
            profile.AddAddress("Testowa", "5", null, "12-345", "Warszawa", "PL");
            _repository.Setup(r => r.GetByIdAndUserIdAsync(new UserProfileId(1), "user-1", true)).ReturnsAsync(profile);

            var act = async () => await CreateService().RemoveAddressAsync(1, 0, "user-1");

            await act.Should().ThrowAsync<BusinessException>();
        }

        [Fact]
        public async Task GetDetailsAsync_ExistingProfile_ShouldReturnVm()
        {
            var profile = CreateProfile();
            _repository.Setup(r => r.GetByIdAsync(new UserProfileId(1), false)).ReturnsAsync(profile);

            var result = await CreateService().GetDetailsAsync(1);

            result.Should().NotBeNull();
            result!.FirstName.Should().Be("Jan");
            result.Email.Should().Be("jan@test.com");
        }

        [Fact]
        public async Task GetDetailsAsync_NonExistentProfile_ShouldReturnNull()
        {
            _repository.Setup(r => r.GetByIdAsync(new UserProfileId(99), false)).ReturnsAsync((UserProfile)null);

            var result = await CreateService().GetDetailsAsync(99);

            result.Should().BeNull();
        }

        [Fact]
        public async Task ExistsAsync_WithMatchingIdAndUserId_ShouldReturnTrue()
        {
            _repository.Setup(r => r.ExistsByIdAndUserIdAsync(new UserProfileId(1), "user-1")).ReturnsAsync(true);

            var result = await CreateService().ExistsAsync(1, "user-1");

            result.Should().BeTrue();
        }
    }
}
