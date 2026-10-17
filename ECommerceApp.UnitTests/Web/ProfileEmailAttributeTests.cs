using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using ECommerceApp.Web.Areas.Identity.Pages.Account;
using ECommerceApp.Web.Areas.Identity.Pages.Account.Manage;
using ECommerceApp.Web.Validation;
using AwesomeAssertions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;
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

        [Fact]
        public void AddValidation_ShouldEmitUnobtrusiveProfileEmailAttributes()
        {
            var attributes = new Dictionary<string, string>();
            var context = CreateClientContext(attributes);

            ((IClientModelValidator)_attribute).AddValidation(context);

            attributes.Should().ContainKey("data-val").WhoseValue.Should().Be("true");
            attributes.Should().ContainKey("data-val-profileemail").WhoseValue.Should().Contain("not a valid e-mail address");
        }

        [Fact]
        public void AddValidation_ExistingAttributes_ShouldNotBeOverwritten()
        {
            var attributes = new Dictionary<string, string> { ["data-val-profileemail"] = "custom" };
            var context = CreateClientContext(attributes);

            ((IClientModelValidator)_attribute).AddValidation(context);

            attributes["data-val-profileemail"].Should().Be("custom");
        }

        private static ClientModelValidationContext CreateClientContext(IDictionary<string, string> attributes)
        {
            var metadataProvider = new EmptyModelMetadataProvider();
            var metadata = metadataProvider.GetMetadataForProperty(typeof(RegisterModel.InputModel), nameof(RegisterModel.InputModel.Email));
            return new ClientModelValidationContext(new ActionContext(), metadata, metadataProvider, attributes);
        }
    }
}
