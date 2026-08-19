using Shouldly;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using Xunit;

namespace ECommerceApp.Web.IntegrationTests.Presale.Checkout
{
    /// <summary>
    /// ADR-0030 Phase 2 — Guest customer provisioning, exercised through the real HTTP pipeline.
    ///
    /// Closes the gap recorded in
    /// <c>.github/plans/02-phase-guest-customer-provisioning-validation.md</c> Findings #1: the full
    /// anonymous Cart → AddToCart → PlaceOrder(GET) → PlaceOrder(POST) flow, the no-<c>ApplicationUser</c>
    /// assertion, client-supplied-CustomerId-is-ignored, resubmission idempotency, and the
    /// authenticated-flow regression were previously untested at the HTTP level.
    /// </summary>
    public class GuestCheckoutIntegrationTests
        : GuestCheckoutTestBase, IClassFixture<GuestCheckoutTestFactory>
    {
        public GuestCheckoutIntegrationTests(GuestCheckoutTestFactory factory, ITestOutputHelper output)
            : base(factory, output) { }

        [Fact]
        public async Task GuestCheckout_FullAnonymousFlow_PlacesOrder_CreatesProfile_NoApplicationUser()
        {
            // Arrange
            var client = CreateClient();
            var productId = await _factory.CreateAvailableProductAsync();
            var guestToken = await MintGuestCookieAsync(client);
            var afToken = await FetchAntiForgeryTokenAsync(client, AnonymousTokenSourceUrl);
            var email = UniqueEmail("checkout");

            // Act
            var (orderId, profileId) = await PlaceGuestOrderAsync(client, afToken, productId, email);

            // Assert
            orderId.ShouldBeGreaterThan(0);
            profileId.ShouldBeGreaterThan(0);

            // Order.CustomerId resolves to the guest's real UserProfile row.
            var customerId = await _factory.GetOrderCustomerIdAsync(orderId);
            customerId.ShouldBe(profileId);

            var profile = await _factory.FindProfileByUserIdAsync(guestToken);
            profile.ShouldNotBeNull();
            profile.Id.Value.ShouldBe(profileId);
            profile.Email.Value.ShouldBe(email);

            // No ApplicationUser (ASP.NET Identity) row is ever created for guest checkout.
            (await _factory.ApplicationUserExistsAsync(email)).ShouldBeFalse();
        }

        [Fact]
        public async Task GuestCheckout_ClientSuppliedCustomerId_IsIgnored()
        {
            // Arrange
            var client = CreateClient();
            var productId = await _factory.CreateAvailableProductAsync();
            await MintGuestCookieAsync(client);
            var afToken = await FetchAntiForgeryTokenAsync(client, AnonymousTokenSourceUrl);
            var email = UniqueEmail("spoofed-customerid");

            const int spoofedCustomerId = 999999;
            // Act
            var (orderId, profileId) = await PlaceGuestOrderAsync(client, afToken, productId, email, customerIdOverride: spoofedCustomerId);

            // Assert
            profileId.ShouldNotBe(spoofedCustomerId);
            (await _factory.GetOrderCustomerIdAsync(orderId)).ShouldBe(profileId);
        }

        [Fact]
        public async Task GuestCheckout_ResubmissionForSameGuestCookie_ReusesSameProfile_NoDuplicate()
        {
            // Arrange
            var client = CreateClient(); // same cookie jar for both orders -> same guest identity
            var productA = await _factory.CreateAvailableProductAsync();
            var productB = await _factory.CreateAvailableProductAsync();
            await MintGuestCookieAsync(client);
            var afToken = await FetchAntiForgeryTokenAsync(client, AnonymousTokenSourceUrl);
            var email = UniqueEmail("resubmit");

            // Act
            var (orderId1, profileId1) = await PlaceGuestOrderAsync(client, afToken, productA, email);

            // The first PlaceOrder POST above mints a GuestAccess sign-in, so the client's identity
            // changes from anonymous to authenticated mid-test. The antiforgery token fetched above
            // embeds the (then-anonymous) identity and is no longer valid for a request made after that
            // sign-in — fetch a fresh one from the Summary page (with the same guest/profileId query
            // string PlaceOrder's own redirect carries, so the CreateAccount form — and therefore the
            // token — actually renders).
            var afTokenAfterSignIn = await FetchAntiForgeryTokenAsync(
                client, $"/Presale/Checkout/Summary/{orderId1}?profileId={profileId1}&guest=True");
            var (orderId2, profileId2) = await PlaceGuestOrderAsync(client, afTokenAfterSignIn, productB, email);

            // Assert
            orderId2.ShouldNotBe(orderId1);
            profileId2.ShouldBe(profileId1, "GetOrCreateForGuestAsync must be idempotent per guest PresaleUserId — a second order for the same guest cookie must not create a second UserProfile");
        }

        [Fact]
        public async Task AuthenticatedCheckout_MissingCustomerId_StillBlocked_Regression()
        {
            // Arrange
            var client = await CreateAuthenticatedClientAsync();
            var token = await FetchAntiForgeryTokenAsync(client, "/Currencies/Currency/Create");
            var form = CreateCustomerForm(token, UniqueEmail("authed-missing-customerid"), "1");
            form.Remove("CustomerId");

            // Act
            var response = await client.PostAsync("/Presale/Checkout/PlaceOrder", new FormUrlEncodedContent(form), CancellationToken);

            // Assert
            response.StatusCode.ShouldBe(HttpStatusCode.OK); // re-renders the form with a ModelState error, not a redirect
            response.RequestMessage!.RequestUri!.AbsolutePath.ShouldNotContain("Summary", Case.Insensitive);
        }

        [Fact]
        public async Task AuthenticatedCheckout_FullFlow_StillSucceeds_Regression()
        {
            // Arrange
            var client = await CreateAuthenticatedClientAsync();
            var productId = await _factory.CreateAvailableProductAsync();
            var profileId = await _factory.EnsureProfileForRegisteredUserAsync(AdminUserId, UniqueEmail("admin-profile"));
            var token = await FetchAntiForgeryTokenAsync(client, "/Currencies/Currency/Create");

            var addForm = CreateAddToCartForm(token, productId);

            // Act
            var addResponse = await client.PostAsync("/Presale/Checkout/AddToCart", new FormUrlEncodedContent(addForm), CancellationToken);
            addResponse.StatusCode.ShouldBe(HttpStatusCode.OK);

            var placeGet = await client.GetAsync("/Presale/Checkout/PlaceOrder", CancellationToken);
            placeGet.StatusCode.ShouldBe(HttpStatusCode.OK);
            placeGet.RequestMessage!.RequestUri!.AbsolutePath.ShouldBe("/Presale/Checkout/PlaceOrder");

            var placeForm = CreateCustomerForm(token, UniqueEmail("admin-order"), "2");
            placeForm["CustomerId"] = profileId.ToString();
            var placePost = await client.PostAsync("/Presale/Checkout/PlaceOrder", new FormUrlEncodedContent(placeForm), CancellationToken);

            // Assert
            placePost.StatusCode.ShouldBe(HttpStatusCode.OK);
            placePost.RequestMessage!.RequestUri!.AbsolutePath.ShouldContain("Summary", Case.Insensitive);

            var (orderId, redirectedProfileId) = ParseSummaryRedirect(placePost.RequestMessage.RequestUri);
            redirectedProfileId.ShouldBe(profileId);
            (await _factory.GetOrderCustomerIdAsync(orderId)).ShouldBe(profileId);
        }

        private static Dictionary<string, string> CreateAddToCartForm(string token, int productId)
            => new()
            {
                ["productId"] = productId.ToString(),
                ["quantity"] = "1",
                ["returnUrl"] = "",
                ["__RequestVerificationToken"] = token
            };

        private static Dictionary<string, string> CreateCustomerForm(string token, string email, string buildingNumber)
            => new()
            {
                ["CurrencyId"] = "1",
                ["FirstName"] = "Jan",
                ["LastName"] = "Kowalski",
                ["Email"] = email,
                ["PhoneNumber"] = "500100200",
                ["IsCompany"] = "false",
                ["Street"] = "Testowa",
                ["BuildingNumber"] = buildingNumber,
                ["ZipCode"] = "00-001",
                ["City"] = "Warszawa",
                ["Country"] = "Polska",
                ["__RequestVerificationToken"] = token
            };
    }
}
