using ECommerceApp.Application.AccountProfile.DTOs;
using ECommerceApp.Application.Identity.IAM.DTOs;
using ECommerceApp.Application.Presale.Checkout.DTOs;
using ECommerceApp.Application.Presale.Checkout.Options;
using ECommerceApp.Application.Supporting.Currencies.DTOs;
using ECommerceApp.API.Options;
using ECommerceApp.Shared.TestInfrastructure;
using Flurl.Http;
using Shouldly;
using System.Net;
using System.Threading.Tasks;
using Xunit;
using Xunit.Abstractions;

namespace ECommerceApp.IntegrationTests.API
{
    /// <summary>
    /// Locks down HTTP-level validation behavior (FluentValidation + [ApiController] auto-400)
    /// so that future refactoring of the validation pipeline cannot silently break the contract.
    ///
    /// Rule: every test asserts the HTTP status code and that the response body is not empty,
    /// which pins both the validation trigger and the error shape without over-specifying the payload.
    /// </summary>
    public class ValidationBehaviorTests : ApiTestBase<ValidationApiTestFactory>, IClassFixture<ValidationApiTestFactory>
    {
        // Seeded Administrator user — matches CustomWebApplicationFactory.GetTokenAsync
        private const string AdminEmail = "test@test";
        private const string AdminPassword = "Test@test12";

        public ValidationBehaviorTests(ValidationApiTestFactory factory, ITestOutputHelper output)
            : base(factory, output) { }

        // ─────────────────────────────────────────────────────────────────
        // Helpers
        // ─────────────────────────────────────────────────────────────────

        private FlurlClient AnonymousClient()
            => new(_factory.CreateClient());

        private async Task<FlurlClient> AuthenticatedClient()
            => await _factory.GetAuthenticatedClient();

        // ─────────────────────────────────────────────────────────────────
        // POST /api/auth/login — SignInDtoValidator
        // ─────────────────────────────────────────────────────────────────

        [Fact]
        public async Task Login_EmptyEmail_Returns400()
        {
            var client = AnonymousClient();
            var dto = new SignInDto("", AdminPassword);

            var response = await client.Request("api/auth/login")
                .AllowAnyHttpStatus()
                .PostJsonAsync(dto);

            response.StatusCode.ShouldBe((int)HttpStatusCode.BadRequest);
            var body = await response.GetStringAsync();
            body.ShouldNotBeNullOrWhiteSpace();
        }

        [Fact]
        public async Task Login_InvalidEmailFormat_Returns400()
        {
            var client = AnonymousClient();
            var dto = new SignInDto("not-an-email", AdminPassword);

            var response = await client.Request("api/auth/login")
                .AllowAnyHttpStatus()
                .PostJsonAsync(dto);

            response.StatusCode.ShouldBe((int)HttpStatusCode.BadRequest);
            var body = await response.GetStringAsync();
            body.ShouldNotBeNullOrWhiteSpace();
        }

        [Fact]
        public async Task Login_EmptyPassword_Returns400()
        {
            var client = AnonymousClient();
            var dto = new SignInDto(AdminEmail, "");

            var response = await client.Request("api/auth/login")
                .AllowAnyHttpStatus()
                .PostJsonAsync(dto);

            response.StatusCode.ShouldBe((int)HttpStatusCode.BadRequest);
            var body = await response.GetStringAsync();
            body.ShouldNotBeNullOrWhiteSpace();
        }

        [Fact]
        public async Task Login_ValidCredentials_Returns200()
        {
            var client = AnonymousClient();
            var dto = new SignInDto(AdminEmail, AdminPassword);

            var response = await client.Request("api/auth/login")
                .AllowAnyHttpStatus()
                .PostJsonAsync(dto);

            response.StatusCode.ShouldBe((int)HttpStatusCode.OK);
        }

        // ─────────────────────────────────────────────────────────────────
        // POST /api/currencies — CreateCurrencyDtoValidator
        // ─────────────────────────────────────────────────────────────────

        [Fact]
        public async Task CreateCurrency_InvalidCode_Returns400()
        {
            var client = await AuthenticatedClient();
            // Code must be exactly 3 chars AND a valid ISO 4217 code
            var dto = new CreateCurrencyDto("XX", "Valid description");

            var response = await client.Request("api/currencies")
                .AllowAnyHttpStatus()
                .PostJsonAsync(dto);

            response.StatusCode.ShouldBe((int)HttpStatusCode.BadRequest);
            var body = await response.GetStringAsync();
            body.ShouldNotBeNullOrWhiteSpace();
        }

        [Fact]
        public async Task CreateCurrency_EmptyDescription_Returns400()
        {
            var client = await AuthenticatedClient();
            var dto = new CreateCurrencyDto("EUR", "");

            var response = await client.Request("api/currencies")
                .AllowAnyHttpStatus()
                .PostJsonAsync(dto);

            response.StatusCode.ShouldBe((int)HttpStatusCode.BadRequest);
            var body = await response.GetStringAsync();
            body.ShouldNotBeNullOrWhiteSpace();
        }

        [Fact]
        public async Task CreateCurrency_EmptyCode_Returns400()
        {
            var client = await AuthenticatedClient();
            var dto = new CreateCurrencyDto("", "Some description");

            var response = await client.Request("api/currencies")
                .AllowAnyHttpStatus()
                .PostJsonAsync(dto);

            response.StatusCode.ShouldBe((int)HttpStatusCode.BadRequest);
            var body = await response.GetStringAsync();
            body.ShouldNotBeNullOrWhiteSpace();
        }

        // ─────────────────────────────────────────────────────────────────
        // POST /api/cart/items — AddToCartDtoValidator + MaxApiQuantityFilter
        // ─────────────────────────────────────────────────────────────────

        [Fact]
        public async Task AddToCart_ZeroQuantity_Returns400()
        {
            var client = await AuthenticatedClient();
            // UserId and ProductId don't matter here; validation fires on Quantity first
            var dto = new AddToCartDto("a85e6eb8-242d-4bbe-9ce6-b2fbb2ddbb4e", 1, 0);

            var response = await client.Request("api/cart/items")
                .AllowAnyHttpStatus()
                .PostJsonAsync(dto);

            response.StatusCode.ShouldBe((int)HttpStatusCode.BadRequest);
            var body = await response.GetStringAsync();
            body.ShouldNotBeNullOrWhiteSpace();
        }

        [Fact]
        public async Task AddToCart_NegativeQuantity_Returns400()
        {
            var client = await AuthenticatedClient();
            var dto = new AddToCartDto("a85e6eb8-242d-4bbe-9ce6-b2fbb2ddbb4e", 1, -5);

            var response = await client.Request("api/cart/items")
                .AllowAnyHttpStatus()
                .PostJsonAsync(dto);

            response.StatusCode.ShouldBe((int)HttpStatusCode.BadRequest);
            var body = await response.GetStringAsync();
            body.ShouldNotBeNullOrWhiteSpace();
        }

        [Fact]
        public async Task AddToCart_QuantityExceedsWebLimit_Returns400()
        {
            var client = await AuthenticatedClient();
            // CheckoutOptions.MaxWebQuantityPerOrderLine = 10; use 11 to trigger the rule
            var dto = new AddToCartDto("a85e6eb8-242d-4bbe-9ce6-b2fbb2ddbb4e", 1, CheckoutOptions.MaxWebQuantityPerOrderLine + 1);

            var response = await client.Request("api/cart/items")
                .AllowAnyHttpStatus()
                .PostJsonAsync(dto);

            response.StatusCode.ShouldBe((int)HttpStatusCode.BadRequest);
            var body = await response.GetStringAsync();
            body.ShouldNotBeNullOrWhiteSpace();
        }

        [Fact]
        public async Task AddToCart_QuantityExceedsApiLimit_Returns400()
        {
            var client = await AuthenticatedClient();
            // MaxApiQuantityFilter kicks in at ApiPurchaseOptions.MaxQuantityPerOrderLine = 5
            var dto = new AddToCartDto("a85e6eb8-242d-4bbe-9ce6-b2fbb2ddbb4e", 1, ApiPurchaseOptions.MaxQuantityPerOrderLine + 1);

            var response = await client.Request("api/cart/items")
                .AllowAnyHttpStatus()
                .PostJsonAsync(dto);

            response.StatusCode.ShouldBe((int)HttpStatusCode.BadRequest);
            var body = await response.GetStringAsync();
            body.ShouldNotBeNullOrWhiteSpace();
        }

        // ─────────────────────────────────────────────────────────────────
        // PUT /api/currencies — UpdateCurrencyDtoValidator
        // ─────────────────────────────────────────────────────────────────

        [Fact]
        public async Task UpdateCurrency_InvalidCode_Returns400()
        {
            var client = await AuthenticatedClient();
            var dto = new UpdateCurrencyDto(2, "XX", "Valid description");

            var response = await client.Request("api/currencies")
                .AllowAnyHttpStatus()
                .PutJsonAsync(dto);

            response.StatusCode.ShouldBe((int)HttpStatusCode.BadRequest);
            var body = await response.GetStringAsync();
            body.ShouldNotBeNullOrWhiteSpace();
        }

        [Fact]
        public async Task UpdateCurrency_ZeroId_Returns400()
        {
            var client = await AuthenticatedClient();
            var dto = new UpdateCurrencyDto(0, "EUR", "Euro");

            var response = await client.Request("api/currencies")
                .AllowAnyHttpStatus()
                .PutJsonAsync(dto);

            response.StatusCode.ShouldBe((int)HttpStatusCode.BadRequest);
            var body = await response.GetStringAsync();
            body.ShouldNotBeNullOrWhiteSpace();
        }

        // ─────────────────────────────────────────────────────────────────
        // POST /api/customers — CreateUserProfileDtoValidator
        // ─────────────────────────────────────────────────────────────────

        [Fact]
        public async Task CreateUserProfile_EmptyFirstName_Returns400()
        {
            var client = await AuthenticatedClient();
            var dto = new CreateUserProfileDto(
                UserId: "a85e6eb8-242d-4bbe-9ce6-b2fbb2ddbb4e",
                FirstName: "",
                LastName: "Tester",
                IsCompany: false,
                NIP: null,
                CompanyName: null,
                Email: "valid@test.com",
                PhoneNumber: "123456789");

            var response = await client.Request("api/customers")
                .AllowAnyHttpStatus()
                .PostJsonAsync(dto);

            response.StatusCode.ShouldBe((int)HttpStatusCode.BadRequest);
            var body = await response.GetStringAsync();
            body.ShouldNotBeNullOrWhiteSpace();
        }

        [Fact]
        public async Task CreateUserProfile_InvalidEmail_Returns400()
        {
            var client = await AuthenticatedClient();
            var dto = new CreateUserProfileDto(
                UserId: "a85e6eb8-242d-4bbe-9ce6-b2fbb2ddbb4e",
                FirstName: "Test",
                LastName: "Tester",
                IsCompany: false,
                NIP: null,
                CompanyName: null,
                Email: "not-an-email",
                PhoneNumber: "123456789");

            var response = await client.Request("api/customers")
                .AllowAnyHttpStatus()
                .PostJsonAsync(dto);

            response.StatusCode.ShouldBe((int)HttpStatusCode.BadRequest);
            var body = await response.GetStringAsync();
            body.ShouldNotBeNullOrWhiteSpace();
        }

        [Fact]
        public async Task CreateUserProfile_CompanyWithoutCompanyName_Returns400()
        {
            var client = await AuthenticatedClient();
            // IsCompany=true triggers the conditional RuleFor(CompanyName).NotEmpty()
            var dto = new CreateUserProfileDto(
                UserId: "a85e6eb8-242d-4bbe-9ce6-b2fbb2ddbb4e",
                FirstName: "Test",
                LastName: "Tester",
                IsCompany: true,
                NIP: null,
                CompanyName: null,
                Email: "valid@test.com",
                PhoneNumber: "123456789");

            var response = await client.Request("api/customers")
                .AllowAnyHttpStatus()
                .PostJsonAsync(dto);

            response.StatusCode.ShouldBe((int)HttpStatusCode.BadRequest);
            var body = await response.GetStringAsync();
            body.ShouldNotBeNullOrWhiteSpace();
        }

        // ─────────────────────────────────────────────────────────────────
        // PUT /api/customers/{id} — UpdateUserProfileDtoValidator
        // ─────────────────────────────────────────────────────────────────

        [Fact]
        public async Task UpdateUserProfile_EmptyLastName_Returns400()
        {
            var client = await AuthenticatedClient();
            var dto = new UpdateUserProfileDto(
                Id: 1,
                FirstName: "Test",
                LastName: "",
                IsCompany: false,
                NIP: null,
                CompanyName: null);

            var response = await client.Request("api/customers/1")
                .AllowAnyHttpStatus()
                .PutJsonAsync(dto);

            response.StatusCode.ShouldBe((int)HttpStatusCode.BadRequest);
            var body = await response.GetStringAsync();
            body.ShouldNotBeNullOrWhiteSpace();
        }

        // ─────────────────────────────────────────────────────────────────
        // POST /api/addresses — AddAddressDtoValidator
        // ─────────────────────────────────────────────────────────────────

        [Fact]
        public async Task AddAddress_EmptyStreet_Returns400()
        {
            var client = await AuthenticatedClient();
            var dto = new AddAddressDto(
                UserProfileId: 1,
                Street: "",
                BuildingNumber: "10",
                FlatNumber: null,
                ZipCode: "12-345",
                City: "Warsaw",
                Country: "PL");

            var response = await client.Request("api/addresses")
                .AllowAnyHttpStatus()
                .PostJsonAsync(dto);

            response.StatusCode.ShouldBe((int)HttpStatusCode.BadRequest);
            var body = await response.GetStringAsync();
            body.ShouldNotBeNullOrWhiteSpace();
        }

        [Fact]
        public async Task AddAddress_InvalidCountryLength_Returns400()
        {
            var client = await AuthenticatedClient();
            // Country must be exactly 2 chars
            var dto = new AddAddressDto(
                UserProfileId: 1,
                Street: "Main St",
                BuildingNumber: "10",
                FlatNumber: null,
                ZipCode: "12-345",
                City: "Warsaw",
                Country: "Poland");

            var response = await client.Request("api/addresses")
                .AllowAnyHttpStatus()
                .PostJsonAsync(dto);

            response.StatusCode.ShouldBe((int)HttpStatusCode.BadRequest);
            var body = await response.GetStringAsync();
            body.ShouldNotBeNullOrWhiteSpace();
        }

        // ─────────────────────────────────────────────────────────────────
        // PUT /api/addresses/{profileId} — UpdateAddressDtoValidator
        // ─────────────────────────────────────────────────────────────────

        [Fact]
        public async Task UpdateAddress_ZeroAddressId_Returns400()
        {
            var client = await AuthenticatedClient();
            var dto = new UpdateAddressDto(
                AddressId: 0,
                Street: "Main St",
                BuildingNumber: "10",
                FlatNumber: null,
                ZipCode: "12-345",
                City: "Warsaw",
                Country: "PL");

            var response = await client.Request("api/addresses/1")
                .AllowAnyHttpStatus()
                .PutJsonAsync(dto);

            response.StatusCode.ShouldBe((int)HttpStatusCode.BadRequest);
            var body = await response.GetStringAsync();
            body.ShouldNotBeNullOrWhiteSpace();
        }

        [Fact]
        public async Task UpdateAddress_EmptyStreet_Returns400()
        {
            var client = await AuthenticatedClient();
            var dto = new UpdateAddressDto(
                AddressId: 1,
                Street: "",
                BuildingNumber: "10",
                FlatNumber: null,
                ZipCode: "12-345",
                City: "Warsaw",
                Country: "PL");

            var response = await client.Request("api/addresses/1")
                .AllowAnyHttpStatus()
                .PutJsonAsync(dto);

            response.StatusCode.ShouldBe((int)HttpStatusCode.BadRequest);
            var body = await response.GetStringAsync();
            body.ShouldNotBeNullOrWhiteSpace();
        }

        [Fact]
        public async Task UpdateAddress_InvalidCountryLength_Returns400()
        {
            var client = await AuthenticatedClient();
            // Country must be exactly 2 chars
            var dto = new UpdateAddressDto(
                AddressId: 1,
                Street: "Main St",
                BuildingNumber: "10",
                FlatNumber: null,
                ZipCode: "12-345",
                City: "Warsaw",
                Country: "Poland");

            var response = await client.Request("api/addresses/1")
                .AllowAnyHttpStatus()
                .PutJsonAsync(dto);

            response.StatusCode.ShouldBe((int)HttpStatusCode.BadRequest);
            var body = await response.GetStringAsync();
            body.ShouldNotBeNullOrWhiteSpace();
        }

        [Fact]
        public async Task AddAddress_EmptyBuildingNumber_Returns400()
        {
            var client = await AuthenticatedClient();
            var dto = new AddAddressDto(
                UserProfileId: 1,
                Street: "Main St",
                BuildingNumber: "",
                FlatNumber: null,
                ZipCode: "12-345",
                City: "Warsaw",
                Country: "PL");

            var response = await client.Request("api/addresses")
                .AllowAnyHttpStatus()
                .PostJsonAsync(dto);

            response.StatusCode.ShouldBe((int)HttpStatusCode.BadRequest);
            var body = await response.GetStringAsync();
            body.ShouldNotBeNullOrWhiteSpace();
        }

        [Fact]
        public async Task AddAddress_EmptyCity_Returns400()
        {
            var client = await AuthenticatedClient();
            var dto = new AddAddressDto(
                UserProfileId: 1,
                Street: "Main St",
                BuildingNumber: "10",
                FlatNumber: null,
                ZipCode: "12-345",
                City: "",
                Country: "PL");

            var response = await client.Request("api/addresses")
                .AllowAnyHttpStatus()
                .PostJsonAsync(dto);

            response.StatusCode.ShouldBe((int)HttpStatusCode.BadRequest);
            var body = await response.GetStringAsync();
            body.ShouldNotBeNullOrWhiteSpace();
        }

        [Fact]
        public async Task AddAddress_ZeroUserProfileId_Returns400()
        {
            var client = await AuthenticatedClient();
            var dto = new AddAddressDto(
                UserProfileId: 0,
                Street: "Main St",
                BuildingNumber: "10",
                FlatNumber: null,
                ZipCode: "12-345",
                City: "Warsaw",
                Country: "PL");

            var response = await client.Request("api/addresses")
                .AllowAnyHttpStatus()
                .PostJsonAsync(dto);

            response.StatusCode.ShouldBe((int)HttpStatusCode.BadRequest);
            var body = await response.GetStringAsync();
            body.ShouldNotBeNullOrWhiteSpace();
        }

        // ─────────────────────────────────────────────────────────────────
        // POST /api/customers — CreateUserProfileDtoValidator (extra rules)
        // ─────────────────────────────────────────────────────────────────

        [Fact]
        public async Task CreateUserProfile_EmptyUserId_Returns400()
        {
            var client = await AuthenticatedClient();
            var dto = new CreateUserProfileDto(
                UserId: "",
                FirstName: "Test",
                LastName: "Tester",
                IsCompany: false,
                NIP: null,
                CompanyName: null,
                Email: "valid@test.com",
                PhoneNumber: "123456789");

            var response = await client.Request("api/customers")
                .AllowAnyHttpStatus()
                .PostJsonAsync(dto);

            response.StatusCode.ShouldBe((int)HttpStatusCode.BadRequest);
            var body = await response.GetStringAsync();
            body.ShouldNotBeNullOrWhiteSpace();
        }

        [Fact]
        public async Task CreateUserProfile_EmptyLastName_Returns400()
        {
            var client = await AuthenticatedClient();
            var dto = new CreateUserProfileDto(
                UserId: "a85e6eb8-242d-4bbe-9ce6-b2fbb2ddbb4e",
                FirstName: "Test",
                LastName: "",
                IsCompany: false,
                NIP: null,
                CompanyName: null,
                Email: "valid@test.com",
                PhoneNumber: "123456789");

            var response = await client.Request("api/customers")
                .AllowAnyHttpStatus()
                .PostJsonAsync(dto);

            response.StatusCode.ShouldBe((int)HttpStatusCode.BadRequest);
            var body = await response.GetStringAsync();
            body.ShouldNotBeNullOrWhiteSpace();
        }

        [Fact]
        public async Task CreateUserProfile_EmptyPhoneNumber_Returns400()
        {
            var client = await AuthenticatedClient();
            var dto = new CreateUserProfileDto(
                UserId: "a85e6eb8-242d-4bbe-9ce6-b2fbb2ddbb4e",
                FirstName: "Test",
                LastName: "Tester",
                IsCompany: false,
                NIP: null,
                CompanyName: null,
                Email: "valid@test.com",
                PhoneNumber: "");

            var response = await client.Request("api/customers")
                .AllowAnyHttpStatus()
                .PostJsonAsync(dto);

            response.StatusCode.ShouldBe((int)HttpStatusCode.BadRequest);
            var body = await response.GetStringAsync();
            body.ShouldNotBeNullOrWhiteSpace();
        }

        // ─────────────────────────────────────────────────────────────────
        // PUT /api/customers/{id} — UpdateUserProfileDtoValidator (extra rules)
        // ─────────────────────────────────────────────────────────────────

        [Fact]
        public async Task UpdateUserProfile_EmptyFirstName_Returns400()
        {
            var client = await AuthenticatedClient();
            var dto = new UpdateUserProfileDto(
                Id: 1,
                FirstName: "",
                LastName: "Tester",
                IsCompany: false,
                NIP: null,
                CompanyName: null);

            var response = await client.Request("api/customers/1")
                .AllowAnyHttpStatus()
                .PutJsonAsync(dto);

            response.StatusCode.ShouldBe((int)HttpStatusCode.BadRequest);
            var body = await response.GetStringAsync();
            body.ShouldNotBeNullOrWhiteSpace();
        }

        [Fact]
        public async Task UpdateUserProfile_ZeroId_Returns400()
        {
            var client = await AuthenticatedClient();
            var dto = new UpdateUserProfileDto(
                Id: 0,
                FirstName: "Test",
                LastName: "Tester",
                IsCompany: false,
                NIP: null,
                CompanyName: null);

            var response = await client.Request("api/customers/0")
                .AllowAnyHttpStatus()
                .PutJsonAsync(dto);

            response.StatusCode.ShouldBe((int)HttpStatusCode.BadRequest);
            var body = await response.GetStringAsync();
            body.ShouldNotBeNullOrWhiteSpace();
        }

        [Fact]
        public async Task UpdateUserProfile_CompanyWithoutCompanyName_Returns400()
        {
            var client = await AuthenticatedClient();
            var dto = new UpdateUserProfileDto(
                Id: 1,
                FirstName: "Test",
                LastName: "Tester",
                IsCompany: true,
                NIP: null,
                CompanyName: null);

            var response = await client.Request("api/customers/1")
                .AllowAnyHttpStatus()
                .PutJsonAsync(dto);

            response.StatusCode.ShouldBe((int)HttpStatusCode.BadRequest);
            var body = await response.GetStringAsync();
            body.ShouldNotBeNullOrWhiteSpace();
        }

        // ─────────────────────────────────────────────────────────────────
        // PUT /api/currencies — UpdateCurrencyDtoValidator (extra rules)
        // ─────────────────────────────────────────────────────────────────

        [Fact]
        public async Task UpdateCurrency_EmptyDescription_Returns400()
        {
            var client = await AuthenticatedClient();
            var dto = new UpdateCurrencyDto(2, "EUR", "");

            var response = await client.Request("api/currencies")
                .AllowAnyHttpStatus()
                .PutJsonAsync(dto);

            response.StatusCode.ShouldBe((int)HttpStatusCode.BadRequest);
            var body = await response.GetStringAsync();
            body.ShouldNotBeNullOrWhiteSpace();
        }

        // ─────────────────────────────────────────────────────────────────
        // POST /api/cart/items — AddToCartDtoValidator (extra rules)
        // ─────────────────────────────────────────────────────────────────

        [Fact]
        public async Task AddToCart_EmptyUserId_Returns400()
        {
            var client = await AuthenticatedClient();
            var dto = new AddToCartDto("", 1, 1);

            var response = await client.Request("api/cart/items")
                .AllowAnyHttpStatus()
                .PostJsonAsync(dto);

            response.StatusCode.ShouldBe((int)HttpStatusCode.BadRequest);
            var body = await response.GetStringAsync();
            body.ShouldNotBeNullOrWhiteSpace();
        }
    }
}

