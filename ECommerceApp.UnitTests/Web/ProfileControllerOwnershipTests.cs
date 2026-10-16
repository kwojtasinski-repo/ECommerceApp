using System.Security.Claims;
using System.Threading.Tasks;
using AwesomeAssertions;
using ECommerceApp.Application.AccountProfile.DTOs;
using ECommerceApp.Application.AccountProfile.Services;
using ECommerceApp.Application.AccountProfile.ViewModels;
using ECommerceApp.Web.Areas.AccountProfile.Controllers;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using Xunit;

namespace ECommerceApp.UnitTests.Web
{
    /// <summary>
    /// Ownership (IDOR) guard on the profile mutation endpoints: only the profile owner or a
    /// maintenance role may edit a profile; everyone else gets Forbid and the service is never called.
    /// </summary>
    public class ProfileControllerOwnershipTests
    {
        private const int ProfileId = 7;
        private const string OwnerId = "owner-1";
        private const string OtherUserId = "intruder-2";

        private readonly Mock<IUserProfileService> _service = new();

        private ProfileController CreateController(string userId, params string[] roles)
        {
            var identity = new ClaimsIdentity(
                new[] { new Claim(ClaimTypes.NameIdentifier, userId) }, "test");
            foreach (var role in roles)
            {
                identity.AddClaim(new Claim(ClaimTypes.Role, role));
            }

            return new ProfileController(_service.Object)
            {
                ControllerContext = new ControllerContext
                {
                    HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(identity) }
                }
            };
        }

        private void SetupProfileOwnedBy(string ownerUserId)
        {
            _service.Setup(s => s.GetDetailsAsync(ProfileId))
                .ReturnsAsync(new UserProfileDetailsVm { Id = ProfileId, UserId = ownerUserId });
            _service.Setup(s => s.UpdatePersonalInfoAsync(It.IsAny<UpdateUserProfileDto>())).ReturnsAsync(true);
            _service.Setup(s => s.UpdateContactInfoAsync(It.IsAny<UpdateContactInfoDto>())).ReturnsAsync(true);
        }

        private void SetupProfileMissing()
        {
            _service.Setup(s => s.GetDetailsAsync(ProfileId)).ReturnsAsync((UserProfileDetailsVm)null);
        }

        private static EditProfileFormVm NewPersonalInfo() => new()
        {
            FirstName = "Jan",
            LastName = "Nowak",
            IsCompany = false
        };

        [Fact]
        public async Task Edit_WhenCallerIsNotOwner_ReturnsForbidAndDoesNotUpdate()
        {
            SetupProfileOwnedBy(OwnerId);
            var controller = CreateController(OtherUserId);

            var result = await controller.Edit(ProfileId, NewPersonalInfo());

            result.Should().BeOfType<ForbidResult>();
            _service.Verify(s => s.UpdatePersonalInfoAsync(It.IsAny<UpdateUserProfileDto>()), Times.Never);
        }

        [Fact]
        public async Task Edit_WhenCallerIsOwner_UpdatesAndRedirects()
        {
            SetupProfileOwnedBy(OwnerId);
            var controller = CreateController(OwnerId);

            var result = await controller.Edit(ProfileId, NewPersonalInfo());

            result.Should().BeOfType<RedirectToActionResult>();
            _service.Verify(s => s.UpdatePersonalInfoAsync(
                It.Is<UpdateUserProfileDto>(d => d.Id == ProfileId && d.FirstName == "Jan")), Times.Once);
        }

        [Theory]
        [InlineData("Administrator")]
        [InlineData("Manager")]
        [InlineData("Service")]
        public async Task Edit_WhenCallerIsMaintenanceRole_UpdatesAndRedirects(string role)
        {
            SetupProfileOwnedBy(OwnerId);
            var controller = CreateController(OtherUserId, role);

            var result = await controller.Edit(ProfileId, NewPersonalInfo());

            result.Should().BeOfType<RedirectToActionResult>();
            _service.Verify(s => s.UpdatePersonalInfoAsync(It.IsAny<UpdateUserProfileDto>()), Times.Once);
        }

        [Fact]
        public async Task Edit_WhenProfileMissing_ReturnsNotFoundAndDoesNotUpdate()
        {
            SetupProfileMissing();
            var controller = CreateController(OwnerId);

            var result = await controller.Edit(ProfileId, NewPersonalInfo());

            result.Should().BeOfType<NotFoundResult>();
            _service.Verify(s => s.UpdatePersonalInfoAsync(It.IsAny<UpdateUserProfileDto>()), Times.Never);
        }

        [Fact]
        public async Task EditContactInfo_WhenCallerIsNotOwner_ReturnsForbidAndDoesNotUpdate()
        {
            SetupProfileOwnedBy(OwnerId);
            var controller = CreateController(OtherUserId);

            var result = await controller.EditContactInfo(ProfileId, "evil@test.com", "999");

            result.Should().BeOfType<ForbidResult>();
            _service.Verify(s => s.UpdateContactInfoAsync(It.IsAny<UpdateContactInfoDto>()), Times.Never);
        }

        [Fact]
        public async Task EditContactInfo_WhenCallerIsOwner_UpdatesAndRedirects()
        {
            SetupProfileOwnedBy(OwnerId);
            var controller = CreateController(OwnerId);

            var result = await controller.EditContactInfo(ProfileId, "me@test.com", "123");

            result.Should().BeOfType<RedirectToActionResult>();
            _service.Verify(s => s.UpdateContactInfoAsync(
                It.Is<UpdateContactInfoDto>(d => d.Id == ProfileId && d.Email == "me@test.com")), Times.Once);
        }

        [Theory]
        [InlineData("Administrator")]
        [InlineData("Manager")]
        [InlineData("Service")]
        public async Task EditContactInfo_WhenCallerIsMaintenanceRole_UpdatesAndRedirects(string role)
        {
            SetupProfileOwnedBy(OwnerId);
            var controller = CreateController(OtherUserId, role);

            var result = await controller.EditContactInfo(ProfileId, "staff@test.com", "123");

            result.Should().BeOfType<RedirectToActionResult>();
            _service.Verify(s => s.UpdateContactInfoAsync(It.IsAny<UpdateContactInfoDto>()), Times.Once);
        }

        [Fact]
        public async Task EditContactInfo_WhenProfileMissing_ReturnsNotFoundAndDoesNotUpdate()
        {
            SetupProfileMissing();
            var controller = CreateController(OwnerId);

            var result = await controller.EditContactInfo(ProfileId, "a@test.com", "1");

            result.Should().BeOfType<NotFoundResult>();
            _service.Verify(s => s.UpdateContactInfoAsync(It.IsAny<UpdateContactInfoDto>()), Times.Never);
        }
    }
}
