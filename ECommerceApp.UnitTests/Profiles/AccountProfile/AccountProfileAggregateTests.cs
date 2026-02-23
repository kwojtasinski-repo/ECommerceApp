using ECommerceApp.Domain.Profiles.AccountProfile;
using FluentAssertions;
using System;
using Xunit;
using AP = ECommerceApp.Domain.Profiles.AccountProfile;

namespace ECommerceApp.UnitTests.Profiles.AccountProfile
{
    public class AccountProfileAggregateTests
    {
        // AccountProfile.Create

        [Fact]
        public void Create_ValidParameters_ShouldReturnProfileAndEvent()
        {
            var (profile, @event) = AP.AccountProfile.Create("user-1", "Jan", "Kowalski", false, null, null);

            profile.UserId.Should().Be("user-1");
            profile.FirstName.Should().Be("Jan");
            profile.LastName.Should().Be("Kowalski");
            profile.IsCompany.Should().BeFalse();
            profile.NIP.Should().BeNull();
            profile.CompanyName.Should().BeNull();
            @event.UserId.Should().Be("user-1");
            @event.OccurredAt.Should().BeCloseTo(DateTime.UtcNow, precision: TimeSpan.FromSeconds(5));
        }

        [Fact]
        public void Create_EmptyUserId_ShouldThrowArgumentException()
        {
            Action act = () => AP.AccountProfile.Create("", "Jan", "Kowalski", false, null, null);

            act.Should().Throw<ArgumentException>().WithMessage("*UserId*");
        }

        [Fact]
        public void Create_EmptyFirstName_ShouldThrowArgumentException()
        {
            Action act = () => AP.AccountProfile.Create("user-1", "", "Kowalski", false, null, null);

            act.Should().Throw<ArgumentException>().WithMessage("*FirstName*");
        }

        [Fact]
        public void Create_EmptyLastName_ShouldThrowArgumentException()
        {
            Action act = () => AP.AccountProfile.Create("user-1", "Jan", "", false, null, null);

            act.Should().Throw<ArgumentException>().WithMessage("*LastName*");
        }

        [Fact]
        public void Create_IsCompanyWithoutCompanyName_ShouldThrowArgumentException()
        {
            Action act = () => AP.AccountProfile.Create("user-1", "Jan", "Kowalski", true, "123", null);

            act.Should().Throw<ArgumentException>().WithMessage("*CompanyName*");
        }

        [Fact]
        public void Create_IsCompanyWithCompanyName_ShouldReturnProfile()
        {
            var (profile, _) = AP.AccountProfile.Create("user-1", "Jan", "Kowalski", true, "12345678901", "Firma XYZ");

            profile.IsCompany.Should().BeTrue();
            profile.CompanyName.Should().Be("Firma XYZ");
        }

        // AccountProfile.UpdatePersonalInfo

        [Fact]
        public void UpdatePersonalInfo_ValidParameters_ShouldUpdateProfile()
        {
            var (profile, _) = AP.AccountProfile.Create("user-1", "Jan", "Kowalski", false, null, null);

            profile.UpdatePersonalInfo("Anna", "Nowak", false, null, null);

            profile.FirstName.Should().Be("Anna");
            profile.LastName.Should().Be("Nowak");
        }

        [Fact]
        public void UpdatePersonalInfo_EmptyFirstName_ShouldThrowArgumentException()
        {
            var (profile, _) = AP.AccountProfile.Create("user-1", "Jan", "Kowalski", false, null, null);

            Action act = () => profile.UpdatePersonalInfo("", "Nowak", false, null, null);

            act.Should().Throw<ArgumentException>().WithMessage("*FirstName*");
        }

        [Fact]
        public void UpdatePersonalInfo_IsCompanyWithoutCompanyName_ShouldThrowArgumentException()
        {
            var (profile, _) = AP.AccountProfile.Create("user-1", "Jan", "Kowalski", false, null, null);

            Action act = () => profile.UpdatePersonalInfo("Jan", "Kowalski", true, null, null);

            act.Should().Throw<ArgumentException>().WithMessage("*CompanyName*");
        }

        // Address.Create

        [Fact]
        public void Address_Create_ValidParameters_ShouldReturnAddress()
        {
            var address = Address.Create(1, "Ul. Testowa", "5", null, 12345, "Warszawa", "Polska");

            address.Street.Should().Be("Ul. Testowa");
            address.ZipCode.Should().Be(12345);
            address.AccountProfileId.Should().Be(1);
        }

        [Fact]
        public void Address_Create_InvalidAccountProfileId_ShouldThrowArgumentException()
        {
            Action act = () => Address.Create(0, "Testowa", "5", null, 12345, "Warszawa", "Polska");

            act.Should().Throw<ArgumentException>().WithMessage("*AccountProfileId*");
        }

        [Fact]
        public void Address_Create_EmptyStreet_ShouldThrowArgumentException()
        {
            Action act = () => Address.Create(1, "", "5", null, 12345, "Warszawa", "Polska");

            act.Should().Throw<ArgumentException>().WithMessage("*Street*");
        }

        [Fact]
        public void Address_Update_ValidParameters_ShouldUpdateAddress()
        {
            var address = Address.Create(1, "Stara", "1", null, 10000, "Gdańsk", "Polska");

            address.Update("Nowa", "2", 3, 20000, "Kraków", "Polska");

            address.Street.Should().Be("Nowa");
            address.BuildingNumber.Should().Be("2");
            address.FlatNumber.Should().Be(3);
            address.ZipCode.Should().Be(20000);
            address.City.Should().Be("Kraków");
        }

        // ContactDetail.Create

        [Fact]
        public void ContactDetail_Create_ValidParameters_ShouldReturnContactDetail()
        {
            var cd = ContactDetail.Create(1, 2, "test@email.com");

            cd.AccountProfileId.Should().Be(1);
            cd.ContactDetailTypeId.Should().Be(2);
            cd.Information.Should().Be("test@email.com");
        }

        [Fact]
        public void ContactDetail_Create_EmptyInformation_ShouldThrowArgumentException()
        {
            Action act = () => ContactDetail.Create(1, 2, "");

            act.Should().Throw<ArgumentException>().WithMessage("*Information*");
        }

        [Fact]
        public void ContactDetail_Update_ValidParameters_ShouldUpdateContactDetail()
        {
            var cd = ContactDetail.Create(1, 2, "old@email.com");

            cd.Update(3, "new@email.com");

            cd.ContactDetailTypeId.Should().Be(3);
            cd.Information.Should().Be("new@email.com");
        }

        // ContactDetailType.Create

        [Fact]
        public void ContactDetailType_Create_ValidName_ShouldReturnType()
        {
            var type = ContactDetailType.Create("Email");

            type.Name.Should().Be("Email");
        }

        [Fact]
        public void ContactDetailType_Create_EmptyName_ShouldThrowArgumentException()
        {
            Action act = () => ContactDetailType.Create("");

            act.Should().Throw<ArgumentException>().WithMessage("*Name*");
        }

        [Fact]
        public void ContactDetailType_UpdateName_ValidName_ShouldUpdateName()
        {
            var type = ContactDetailType.Create("Old");

            type.UpdateName("New");

            type.Name.Should().Be("New");
        }

        [Fact]
        public void ContactDetailType_UpdateName_EmptyName_ShouldThrowArgumentException()
        {
            var type = ContactDetailType.Create("Valid");

            Action act = () => type.UpdateName("");

            act.Should().Throw<ArgumentException>().WithMessage("*Name*");
        }
    }
}
