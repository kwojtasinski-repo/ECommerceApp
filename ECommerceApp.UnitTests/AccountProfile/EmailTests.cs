using ECommerceApp.Domain.AccountProfile.ValueObjects;
using AwesomeAssertions;
using System;
using Xunit;

namespace ECommerceApp.UnitTests.AccountProfile
{
    public class EmailTests
    {
        [Theory]
        [InlineData("user@example.com")]
        [InlineData("User@Example.COM")]
        public void IsValid_WellFormedAddress_ShouldReturnTrue(string value)
        {
            Email.IsValid(value).Should().BeTrue();
        }

        [Theory]
        [InlineData("user@localhost")]
        [InlineData("a@b")]
        [InlineData("")]
        [InlineData("   ")]
        [InlineData(null)]
        [InlineData("user @example.com")]
        [InlineData("user@exa mple.com")]
        public void IsValid_MalformedAddress_ShouldReturnFalse(string value)
        {
            Email.IsValid(value).Should().BeFalse();
        }

        [Fact]
        public void Constructor_UpperCaseAddress_ShouldNormalizeToLowerCase()
        {
            new Email("User@Example.COM").Value.Should().Be("user@example.com");
        }

        [Fact]
        public void Constructor_EmptyValue_ShouldThrowArgumentException()
        {
            Action act = () => new Email(" ");

            act.Should().Throw<ArgumentException>().WithMessage("Email cannot be empty*");
        }

        [Fact]
        public void Constructor_NoDotInDomain_ShouldThrowArgumentException()
        {
            Action act = () => new Email("user@localhost");

            act.Should().Throw<ArgumentException>().WithMessage("Email format is invalid*");
        }
    }
}
