using System.ComponentModel.DataAnnotations;
using ECommerceApp.Web.Areas.Identity.Pages.Account;
using ECommerceApp.Web.Areas.Identity.Pages.Account.Manage;
using ECommerceApp.Web.Validation;
using AwesomeAssertions;
using Xunit;

namespace ECommerceApp.UnitTests.Web
{
    public class ProfileEmailAttributeTests
    {
        private readonly ProfileEmailAttribute _attribute = new();

        [Theory]
        [InlineData("user@example.com")]
        [InlineData("User@Example.COM")]
        public void IsValid_WellFormedAddress_ShouldReturnTrue(string value)
        {
            _attribute.IsValid(value).Should().BeTrue();
        }

        [Theory]
        [InlineData("user@localhost")]
        [InlineData("a@b")]
        [InlineData("")]
        [InlineData("user @example.com")]
        public void IsValid_MalformedAddress_ShouldReturnFalse(string value)
        {
            _attribute.IsValid(value).Should().BeFalse();
        }

        [Fact]
        public void RegisterInput_LocalhostAddress_ShouldFailModelValidation()
        {
            var input = new RegisterModel.InputModel { Email = "user@localhost", Password = "secret1", ConfirmPassword = "secret1" };

            Validator.TryValidateObject(input, new ValidationContext(input), null, true).Should().BeFalse();
        }

        [Fact]
        public void ManageEmailInput_LocalhostAddress_ShouldFailModelValidation()
        {
            var input = new EmailModel.InputModel { NewEmail = "user@localhost" };

            Validator.TryValidateObject(input, new ValidationContext(input), null, true).Should().BeFalse();
        }

        [Fact]
        public void ManageEmailInput_ValidAddress_ShouldPassModelValidation()
        {
            var input = new EmailModel.InputModel { NewEmail = "user@example.com" };

            Validator.TryValidateObject(input, new ValidationContext(input), null, true).Should().BeTrue();
        }
    }
}
