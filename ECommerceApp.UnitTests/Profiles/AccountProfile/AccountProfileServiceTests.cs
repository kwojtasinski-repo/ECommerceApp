using AutoMapper;
using AutoMapper.Internal;
using ECommerceApp.Application.Exceptions;
using ECommerceApp.Application.Mapping;
using ECommerceApp.Application.Profiles.AccountProfile.DTOs;
using ECommerceApp.Application.Profiles.AccountProfile.Services;
using ECommerceApp.Domain.Profiles.AccountProfile;
using FluentAssertions;
using Moq;
using System.Threading.Tasks;
using Xunit;
using AP = ECommerceApp.Domain.Profiles.AccountProfile;

namespace ECommerceApp.UnitTests.Profiles.AccountProfile
{
    public class AccountProfileServiceTests
    {
        private readonly Mock<IAccountProfileRepository> _repository;
        private readonly IMapper _mapper;

        public AccountProfileServiceTests()
        {
            _repository = new Mock<IAccountProfileRepository>();
            var config = new MapperConfiguration(cfg =>
            {
                cfg.AddProfile<MappingProfile>();
                cfg.Internal().MethodMappingEnabled = false;
            });
            _mapper = config.CreateMapper();
        }

        private AccountProfileService CreateService()
            => new(_repository.Object, _mapper);

        [Fact]
        public async Task CreateAsync_ValidDto_ShouldAddProfile()
        {
            var dto = new CreateAccountProfileDto("user-1", "Jan", "Kowalski", false, null, null);
            _repository.Setup(r => r.AddAsync(It.IsAny<AP.AccountProfile>())).ReturnsAsync(1);

            var service = CreateService();
            var result = await service.CreateAsync(dto);

            result.Should().Be(1);
            _repository.Verify(r => r.AddAsync(It.IsAny<AP.AccountProfile>()), Times.Once);
        }

        [Fact]
        public async Task UpdateAsync_ProfileNotFound_ShouldThrowBusinessException()
        {
            var dto = new UpdateAccountProfileDto(99, "Jan", "Kowalski", false, null, null);
            _repository.Setup(r => r.GetByIdAsync(99)).ReturnsAsync((AP.AccountProfile)null);

            var service = CreateService();
            var act = async () => await service.UpdateAsync(dto);

            await act.Should().ThrowAsync<BusinessException>();
        }

        [Fact]
        public async Task UpdateAsync_ValidDto_ShouldUpdateProfile()
        {
            var (profile, _) = AP.AccountProfile.Create("user-1", "Jan", "Kowalski", false, null, null);
            var dto = new UpdateAccountProfileDto(1, "Anna", "Nowak", false, null, null);
            _repository.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(profile);

            var service = CreateService();
            var result = await service.UpdateAsync(dto);

            result.Should().BeTrue();
            _repository.Verify(r => r.UpdateAsync(It.IsAny<AP.AccountProfile>()), Times.Once);
        }

        [Fact]
        public async Task ExistsAsync_WithMatchingIdAndUserId_ShouldReturnTrue()
        {
            _repository.Setup(r => r.ExistsByIdAndUserIdAsync(1, "user-1")).ReturnsAsync(true);

            var service = CreateService();
            var result = await service.ExistsAsync(1, "user-1");

            result.Should().BeTrue();
        }

        [Fact]
        public async Task DeleteAsync_NonExistentProfile_ShouldReturnFalse()
        {
            _repository.Setup(r => r.DeleteAsync(99)).ReturnsAsync(false);

            var service = CreateService();
            var result = await service.DeleteAsync(99);

            result.Should().BeFalse();
        }

        [Fact]
        public async Task GetDetailsAsync_ExistingId_ShouldReturnVm()
        {
            var (profile, _) = AP.AccountProfile.Create("user-1", "Jan", "Kowalski", false, null, null);
            _repository.Setup(r => r.GetByIdWithDetailsAsync(1)).ReturnsAsync(profile);

            var service = CreateService();
            var result = await service.GetDetailsAsync(1);

            result.Should().NotBeNull();
            result!.FirstName.Should().Be("Jan");
            result.LastName.Should().Be("Kowalski");
        }

        [Fact]
        public async Task GetDetailsAsync_NonExistentId_ShouldReturnNull()
        {
            _repository.Setup(r => r.GetByIdWithDetailsAsync(99)).ReturnsAsync((AP.AccountProfile)null);

            var service = CreateService();
            var result = await service.GetDetailsAsync(99);

            result.Should().BeNull();
        }
    }
}
