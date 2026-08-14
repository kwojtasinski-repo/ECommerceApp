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
            var client = CreateClient();
            var productId = await _factory.CreateAvailableProductAsync();
            var guestToken = await MintGuestCookieAsync(client);
            var afToken = await FetchAntiForgeryTokenAsync(client, AnonymousTokenSourceUrl);
            var email = UniqueEmail("checkout");

            var (orderId, profileId) = await PlaceGuestOrderAsync(client, afToken, productId, email);

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
            var client = CreateClient();
            var productId = await _factory.CreateAvailableProductAsync();
            await MintGuestCookieAsync(client);
            var afToken = await FetchAntiForgeryTokenAsync(client, AnonymousTokenSourceUrl);
            var email = UniqueEmail("spoofed-customerid");

            const int spoofedCustomerId = 999999;
            var (orderId, profileId) = await PlaceGuestOrderAsync(client, afToken, productId, email, customerIdOverride: spoofedCustomerId);

            profileId.ShouldNotBe(spoofedCustomerId);
            (await _factory.GetOrderCustomerIdAsync(orderId)).ShouldBe(profileId);
        }

        [Fact]
        public async Task GuestCheckout_ResubmissionForSameGuestCookie_ReusesSameProfile_NoDuplicate()
        {
            var client = CreateClient(); // same cookie jar for both orders -> same guest identity
            var productA = await _factory.CreateAvailableProductAsync();
            var productB = await _factory.CreateAvailableProductAsync();
            await MintGuestCookieAsync(client);
            var afToken = await FetchAntiForgeryTokenAsync(client, AnonymousTokenSourceUrl);
            var email = UniqueEmail("resubmit");

            var (orderId1, profileId1) = await PlaceGuestOrderAsync(client, afToken, productA, email);
            var (orderId2, profileId2) = await PlaceGuestOrderAsync(client, afToken, productB, email);

            orderId2.ShouldNotBe(orderId1);
            profileId2.ShouldBe(profileId1, "GetOrCreateForGuestAsync must be idempotent per guest PresaleUserId — a second order for the same guest cookie must not create a second UserProfile");
        }

        [Fact]
        public async Task AuthenticatedCheckout_MissingCustomerId_StillBlocked_Regression()
        {
            var client = await CreateAuthenticatedClientAsync();
            var token = await FetchAntiForgeryTokenAsync(client, "/Currencies/Currency/Create");

            var form = new Dictionary<string, string>
            {
                ["CurrencyId"] = "1",
                ["FirstName"] = "Jan",
                ["LastName"] = "Kowalski",
                ["Email"] = UniqueEmail("authed-missing-customerid"),
                ["PhoneNumber"] = "500100200",
                ["IsCompany"] = "false",
                ["Street"] = "Testowa",
                ["BuildingNumber"] = "1",
                ["ZipCode"] = "00-001",
                ["City"] = "Warszawa",
                ["Country"] = "Polska",
                // CustomerId intentionally omitted.
                ["__RequestVerificationToken"] = token
            };

            var response = await client.PostAsync("/Presale/Checkout/PlaceOrder", new FormUrlEncodedContent(form), CancellationToken);

            response.StatusCode.ShouldBe(HttpStatusCode.OK); // re-renders the form with a ModelState error, not a redirect
            response.RequestMessage!.RequestUri!.AbsolutePath.ShouldNotContain("Summary", Case.Insensitive);
        }

        [Fact]
        public async Task AuthenticatedCheckout_FullFlow_StillSucceeds_Regression()
        {
            var client = await CreateAuthenticatedClientAsync();
            var productId = await _factory.CreateAvailableProductAsync();
            var profileId = await _factory.EnsureProfileForRegisteredUserAsync(AdminUserId, UniqueEmail("admin-profile"));
            var token = await FetchAntiForgeryTokenAsync(client, "/Currencies/Currency/Create");

            var addForm = new Dictionary<string, string>
            {
                ["productId"] = productId.ToString(),
                ["quantity"] = "1",
                ["returnUrl"] = "",
                ["__RequestVerificationToken"] = token
            };
            var addResponse = await client.PostAsync("/Presale/Checkout/AddToCart", new FormUrlEncodedContent(addForm), CancellationToken);
            addResponse.StatusCode.ShouldBe(HttpStatusCode.OK);

            var placeGet = await client.GetAsync("/Presale/Checkout/PlaceOrder", CancellationToken);
            placeGet.StatusCode.ShouldBe(HttpStatusCode.OK);
            placeGet.RequestMessage!.RequestUri!.AbsolutePath.ShouldBe("/Presale/Checkout/PlaceOrder");

            var placeForm = new Dictionary<string, string>
            {
                ["CustomerId"] = profileId.ToString(),
                ["CurrencyId"] = "1",
                ["FirstName"] = "Anna",
                ["LastName"] = "Nowak",
                ["Email"] = UniqueEmail("admin-order"),
                ["PhoneNumber"] = "500200300",
                ["IsCompany"] = "false",
                ["Street"] = "Testowa",
                ["BuildingNumber"] = "2",
                ["ZipCode"] = "00-002",
                ["City"] = "Warszawa",
                ["Country"] = "Polska",
                ["__RequestVerificationToken"] = token
            };
            var placePost = await client.PostAsync("/Presale/Checkout/PlaceOrder", new FormUrlEncodedContent(placeForm), CancellationToken);

            placePost.StatusCode.ShouldBe(HttpStatusCode.OK);
            placePost.RequestMessage!.RequestUri!.AbsolutePath.ShouldContain("Summary", Case.Insensitive);

            var (orderId, redirectedProfileId) = ParseSummaryRedirect(placePost.RequestMessage.RequestUri);
            redirectedProfileId.ShouldBe(profileId);
            (await _factory.GetOrderCustomerIdAsync(orderId)).ShouldBe(profileId);
        }
    }
}
