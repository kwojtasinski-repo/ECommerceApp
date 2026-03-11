using ECommerceApp.Domain.Sales.Orders;
using ECommerceApp.Domain.Shared;
using FluentAssertions;
using Xunit;

namespace ECommerceApp.UnitTests.Sales.Orders
{
    public class OrderCustomerTests
    {
        private static OrderCustomer CreateValid() => new(
            "Jan", "Kowalski", "jan@example.com", "123456789",
            false, null, null,
            "Główna", "1", null, "67-100", "Nowa Sól", "Polska");

        // ── Constructor — valid ───────────────────────────────────────────────

        [Fact]
        public void Constructor_ValidPersonalData_ShouldSetAllProperties()
        {
            var customer = CreateValid();

            customer.FirstName.Should().Be("Jan");
            customer.LastName.Should().Be("Kowalski");
            customer.Email.Should().Be("jan@example.com");
            customer.PhoneNumber.Should().Be("123456789");
            customer.IsCompany.Should().BeFalse();
            customer.CompanyName.Should().BeNull();
            customer.Nip.Should().BeNull();
            customer.Street.Should().Be("Główna");
            customer.BuildingNumber.Should().Be("1");
            customer.FlatNumber.Should().BeNull();
            customer.ZipCode.Should().Be("67-100");
            customer.City.Should().Be("Nowa Sól");
            customer.Country.Should().Be("Polska");
        }

        [Fact]
        public void Constructor_WithCompanyData_ShouldSetCompanyFields()
        {
            var customer = new OrderCustomer(
                "Firma", "Sp.z.o.o.", "firma@example.com", "987654321",
                true, "Przykładowa Sp. z o.o.", "1234567890",
                "Przemysłowa", "5", "2A", "30-100", "Kraków", "Polska");

            customer.IsCompany.Should().BeTrue();
            customer.CompanyName.Should().Be("Przykładowa Sp. z o.o.");
            customer.Nip.Should().Be("1234567890");
        }

        [Fact]
        public void Constructor_WithFlatNumber_ShouldSetFlatNumber()
        {
            var customer = new OrderCustomer(
                "Jan", "Kowalski", "jan@example.com", "123456789",
                false, null, null,
                "Główna", "1", "10", "65-186", "Zielona Góra", "Polska");

            customer.FlatNumber.Should().Be("10");
        }

        // ── Constructor — required field guards ───────────────────────────────

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        public void Constructor_EmptyFirstName_ShouldThrowDomainException(string firstName)
        {
            var act = () => new OrderCustomer(
                firstName, "Kowalski", "jan@example.com", "123456789",
                false, null, null, "Główna", "1", null, "65-186", "Zielona Góra", "Polska");

            act.Should().Throw<DomainException>().WithMessage("*FirstName*");
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        public void Constructor_EmptyLastName_ShouldThrowDomainException(string lastName)
        {
            var act = () => new OrderCustomer(
                "Jan", lastName, "jan@example.com", "123456789",
                false, null, null, "Główna", "1", null, "65-186", "Zielona Góra", "Polska");

            act.Should().Throw<DomainException>().WithMessage("*LastName*");
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        public void Constructor_EmptyEmail_ShouldThrowDomainException(string email)
        {
            var act = () => new OrderCustomer(
                "Jan", "Kowalski", email, "123456789",
                false, null, null, "Główna", "1", null, "65-186", "Zielona Góra", "Polska");

            act.Should().Throw<DomainException>().WithMessage("*Email*");
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        public void Constructor_EmptyPhoneNumber_ShouldThrowDomainException(string phoneNumber)
        {
            var act = () => new OrderCustomer(
                "Jan", "Kowalski", "jan@example.com", phoneNumber,
                false, null, null, "Główna", "1", null, "67-100", "Nowa Sól", "Polska");

            act.Should().Throw<DomainException>().WithMessage("*PhoneNumber*");
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        public void Constructor_EmptyStreet_ShouldThrowDomainException(string street)
        {
            var act = () => new OrderCustomer(
                "Jan", "Kowalski", "jan@example.com", "123456789",
                false, null, null, street, "1", null, "65-186", "Zielona Góra", "Polska");

            act.Should().Throw<DomainException>().WithMessage("*Street*");
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        public void Constructor_EmptyBuildingNumber_ShouldThrowDomainException(string buildingNumber)
        {
            var act = () => new OrderCustomer(
                "Jan", "Kowalski", "jan@example.com", "123456789",
                false, null, null, "Główna", buildingNumber, null, "65-186", "Zielona Góra", "Polska");

            act.Should().Throw<DomainException>().WithMessage("*BuildingNumber*");
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        public void Constructor_EmptyZipCode_ShouldThrowDomainException(string zipCode)
        {
            var act = () => new OrderCustomer(
                "Jan", "Kowalski", "jan@example.com", "123456789",
                false, null, null, "Główna", "1", null, zipCode, "Warszawa", "Polska");

            act.Should().Throw<DomainException>().WithMessage("*ZipCode*");
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        public void Constructor_EmptyCity_ShouldThrowDomainException(string city)
        {
            var act = () => new OrderCustomer(
                "Jan", "Kowalski", "jan@example.com", "123456789",
                false, null, null, "Główna", "1", null, "67-100", city, "Polska");

            act.Should().Throw<DomainException>().WithMessage("*City*");
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        public void Constructor_EmptyCountry_ShouldThrowDomainException(string country)
        {
            var act = () => new OrderCustomer(
                "Jan", "Kowalski", "jan@example.com", "123456789",
                false, null, null, "Główna", "1", null, "67-100", "Nowa Sól", country);

            act.Should().Throw<DomainException>().WithMessage("*Country*");
        }
    }
}
