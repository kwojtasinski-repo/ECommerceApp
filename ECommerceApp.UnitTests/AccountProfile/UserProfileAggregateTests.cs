using ECommerceApp.Domain.AccountProfile;
using FluentAssertions;
using System;
using Xunit;

namespace ECommerceApp.UnitTests.AccountProfile
{
    public class UserProfileAggregateTests
    {
        // UserProfile.Create

        [Fact]
        public void Create_ValidParameters_ShouldReturnProfileAndEvent()
        {
            var (profile, @event) = UserProfile.Create("user-1", "Jan", "Kowalski", false, null, null, "jan@test.com", "123456789");

            profile.UserId.Should().Be("user-1");
            profile.FirstName.Should().Be("Jan");
            profile.LastName.Should().Be("Kowalski");
            profile.IsCompany.Should().BeFalse();
            profile.Email.Should().Be("jan@test.com");
            profile.PhoneNumber.Should().Be("123456789");
            @event.UserId.Should().Be("user-1");
            @event.OccurredAt.Should().BeCloseTo(DateTime.UtcNow, precision: TimeSpan.FromSeconds(5));
        }

        [Fact]
        public void Create_EmptyUserId_ShouldThrowArgumentException()
        {
            Action act = () => UserProfile.Create("", "Jan", "Kowalski", false, null, null, "jan@test.com", "123");

            act.Should().Throw<ArgumentException>().WithMessage("*UserId*");
        }

        [Fact]
        public void Create_EmptyEmail_ShouldThrowArgumentException()
        {
            Action act = () => UserProfile.Create("user-1", "Jan", "Kowalski", false, null, null, "", "123");

            act.Should().Throw<ArgumentException>().WithMessage("*Email*");
        }

        [Fact]
        public void Create_EmptyPhoneNumber_ShouldThrowArgumentException()
        {
            Action act = () => UserProfile.Create("user-1", "Jan", "Kowalski", false, null, null, "jan@test.com", "");

            act.Should().Throw<ArgumentException>().WithMessage("*PhoneNumber*");
        }

        [Fact]
        public void Create_IsCompanyWithoutCompanyName_ShouldThrowArgumentException()
        {
            Action act = () => UserProfile.Create("user-1", "Jan", "Kowalski", true, "123", null, "jan@test.com", "123");

            act.Should().Throw<ArgumentException>().WithMessage("*CompanyName*");
        }

        [Fact]
        public void Create_CompanyProfileWithAllFields_ShouldReturnProfile()
        {
            var (profile, _) = UserProfile.Create("user-1", "Jan", "Kowalski", true, "12345678901", "Firma XYZ", "firma@test.com", "123456789");

            profile.IsCompany.Should().BeTrue();
            profile.CompanyName.Should().Be("Firma XYZ");
        }

        // UpdatePersonalInfo

        [Fact]
        public void UpdatePersonalInfo_ValidParameters_ShouldUpdateProfile()
        {
            var (profile, _) = UserProfile.Create("user-1", "Jan", "Kowalski", false, null, null, "jan@test.com", "123");

            profile.UpdatePersonalInfo("Anna", "Nowak", false, null, null);

            profile.FirstName.Should().Be("Anna");
            profile.LastName.Should().Be("Nowak");
        }

        [Fact]
        public void UpdatePersonalInfo_EmptyFirstName_ShouldThrowArgumentException()
        {
            var (profile, _) = UserProfile.Create("user-1", "Jan", "Kowalski", false, null, null, "jan@test.com", "123");

            Action act = () => profile.UpdatePersonalInfo("", "Nowak", false, null, null);

            act.Should().Throw<ArgumentException>().WithMessage("*FirstName*");
        }

        // UpdateContactInfo

        [Fact]
        public void UpdateContactInfo_ValidParameters_ShouldUpdateEmailAndPhone()
        {
            var (profile, _) = UserProfile.Create("user-1", "Jan", "Kowalski", false, null, null, "old@test.com", "111");

            profile.UpdateContactInfo("new@test.com", "999");

            profile.Email.Should().Be("new@test.com");
            profile.PhoneNumber.Should().Be("999");
        }

        [Fact]
        public void UpdateContactInfo_EmptyEmail_ShouldThrowArgumentException()
        {
            var (profile, _) = UserProfile.Create("user-1", "Jan", "Kowalski", false, null, null, "jan@test.com", "123");

            Action act = () => profile.UpdateContactInfo("", "123");

            act.Should().Throw<ArgumentException>().WithMessage("*Email*");
        }

        // AddAddress

        [Fact]
        public void AddAddress_ValidParameters_ShouldAddAddressToProfile()
        {
            var (profile, _) = UserProfile.Create("user-1", "Jan", "Kowalski", false, null, null, "jan@test.com", "123");

            profile.AddAddress("Testowa", "5", null, 12345, "Warszawa", "Polska");

            profile.Addresses.Should().HaveCount(1);
            profile.Addresses[0].Street.Should().Be("Testowa");
        }

        [Fact]
        public void AddAddress_EmptyStreet_ShouldThrowArgumentException()
        {
            var (profile, _) = UserProfile.Create("user-1", "Jan", "Kowalski", false, null, null, "jan@test.com", "123");

            Action act = () => profile.AddAddress("", "5", null, 12345, "Warszawa", "Polska");

            act.Should().Throw<ArgumentException>().WithMessage("*Street*");
        }

        // UpdateAddress

        [Fact]
        public void UpdateAddress_NonExistentAddressId_ShouldReturnFalse()
        {
            var (profile, _) = UserProfile.Create("user-1", "Jan", "Kowalski", false, null, null, "jan@test.com", "123");

            var result = profile.UpdateAddress(99, "Nowa", "1", null, 10000, "Kraków", "Polska");

            result.Should().BeFalse();
        }

        // RemoveAddress

        [Fact]
        public void RemoveAddress_NonExistentAddressId_ShouldReturnFalse()
        {
            var (profile, _) = UserProfile.Create("user-1", "Jan", "Kowalski", false, null, null, "jan@test.com", "123");

            var result = profile.RemoveAddress(99);

            result.Should().BeFalse();
        }

        // Address.Create (internal — tested via UserProfile.AddAddress)

        [Fact]
        public void AddAddress_InvalidZipCode_ShouldThrowArgumentException()
        {
            var (profile, _) = UserProfile.Create("user-1", "Jan", "Kowalski", false, null, null, "jan@test.com", "123");

            Action act = () => profile.AddAddress("Testowa", "5", null, 0, "Warszawa", "Polska");

            act.Should().Throw<ArgumentException>().WithMessage("*ZipCode*");
        }
    }
}
