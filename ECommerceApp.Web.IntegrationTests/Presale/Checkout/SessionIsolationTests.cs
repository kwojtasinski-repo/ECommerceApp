using Shouldly;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using Xunit;

namespace ECommerceApp.Web.IntegrationTests.Presale.Checkout
{
    /// <summary>
    /// ADR-0030 §12 — the same decoy-session contract is exercised for a guest and an authenticated
    /// session under test. Each session has its own HttpClient cookie jar and its own order data.
    /// </summary>
    public sealed class SessionIsolationTests
        : GuestCheckoutTestBase, IClassFixture<GuestCheckoutTestFactory>
    {
        public SessionIsolationTests(GuestCheckoutTestFactory factory, ITestOutputHelper output)
            : base(factory, output) { }

        [Fact]
        public async Task GuestSession_CannotReadOrActOn_GuestOrAuthenticatedDecoys()
        {
            await AssertCannotCrossSessionAsync(authenticatedUnderTest: false);
        }

        [Fact]
        public async Task AuthenticatedSession_CannotReadOrActOn_GuestOrAuthenticatedDecoys()
        {
            await AssertCannotCrossSessionAsync(authenticatedUnderTest: true);
        }

        /// <summary>
        /// ADR-0030 §12 names Identity/Manage explicitly as an area that must stay authentication-gated.
        /// It has no per-page [Authorize] attribute — it relies on the RazorPagesOptions convention
        /// <c>AddDefaultIdentity</c> registers (<c>AuthorizeAreaFolder("Identity", "/Account/Manage")</c>),
        /// so it can't be checked by attribute reflection like <see cref="GuestCheckoutAllowlistTests"/>
        /// does for MVC controllers — this is the HTTP-level equivalent guard.
        /// </summary>
        [Fact]
        public async Task AnonymousCaller_CannotReachIdentityManage()
        {
            var client = CreateClient(allowAutoRedirect: false);

            var response = await client.GetAsync("/Identity/Account/Manage", CancellationToken);

            response.StatusCode.ShouldBe(HttpStatusCode.Redirect);
            response.Headers.Location!.ToString().ShouldContain(
                "Login", customMessage: "ADR-0030 §12: Identity/Manage must remain fully authentication-gated");
        }

        private async Task AssertCannotCrossSessionAsync(bool authenticatedUnderTest)
        {
            var guestDecoy = CreateClient();
            var guestDecoyProduct = await _factory.CreateAvailableProductAsync();
            var guestDecoyToken = await MintGuestCookieAsync(guestDecoy);
            var guestDecoyAntiForgery = await FetchAntiForgeryTokenAsync(guestDecoy, AnonymousTokenSourceUrl);
            var guestDecoyOrder = await PlaceGuestOrderAsync(
                guestDecoy,
                guestDecoyAntiForgery,
                guestDecoyProduct,
                UniqueEmail("isolation-guest-decoy"));

            var authenticatedDecoyEmail = UniqueEmail("isolation-auth-decoy");
            var authenticatedDecoyPassword = "Isolation@2026";
            var authenticatedDecoyUserId = await _factory.CreateRegisteredUserAsync(
                authenticatedDecoyEmail,
                authenticatedDecoyPassword);
            var authenticatedDecoyProfile = await _factory.EnsureProfileForRegisteredUserAsync(
                authenticatedDecoyUserId,
                authenticatedDecoyEmail);
            var authenticatedDecoy = await LoginAsync(
                authenticatedDecoyEmail,
                authenticatedDecoyPassword);
            var authenticatedDecoyProduct = await _factory.CreateAvailableProductAsync();
            var authenticatedDecoyOrder = await PlaceAuthenticatedOrderAsync(
                authenticatedDecoy,
                authenticatedDecoyProduct,
                authenticatedDecoyProfile);

            HttpClient sessionUnderTest;
            if (authenticatedUnderTest)
            {
                var underTestEmail = UniqueEmail("isolation-auth-under-test");
                var underTestPassword = "Isolation@2026";
                var underTestUserId = await _factory.CreateRegisteredUserAsync(
                    underTestEmail,
                    underTestPassword);
                await _factory.EnsureProfileForRegisteredUserAsync(underTestUserId, underTestEmail);
                sessionUnderTest = await LoginAsync(underTestEmail, underTestPassword);
            }
            else
            {
                sessionUnderTest = CreateClient(allowAutoRedirect: false);
                await MintGuestCookieAsync(sessionUnderTest);
            }

            var cart = await sessionUnderTest.GetAsync("/Presale/Checkout/Cart", CancellationToken);
            var cartHtml = await cart.Content.ReadAsStringAsync(CancellationToken);
            cartHtml.ShouldNotContain($"/offers/{guestDecoyProduct}");
            cartHtml.ShouldNotContain($"/offers/{authenticatedDecoyProduct}");

            await AssertOrderDetailsDeniedAsync(sessionUnderTest, guestDecoyOrder.OrderId, authenticatedUnderTest);
            await AssertOrderDetailsDeniedAsync(sessionUnderTest, authenticatedDecoyOrder.OrderId, authenticatedUnderTest);
            await AssertSummaryDeniedAsync(sessionUnderTest, guestDecoyOrder.OrderId, authenticatedUnderTest);
            await AssertSummaryDeniedAsync(sessionUnderTest, authenticatedDecoyOrder.OrderId, authenticatedUnderTest);

            var antiForgery = await FetchAntiForgeryTokenAsync(sessionUnderTest, AnonymousTokenSourceUrl);
            var promotion = new Dictionary<string, string>
            {
                ["orderId"] = guestDecoyOrder.OrderId.ToString(),
                ["profileId"] = guestDecoyOrder.ProfileId.ToString(),
                ["password"] = "Attacker@2026",
                ["__RequestVerificationToken"] = antiForgery
            };
            var promotionResponse = await sessionUnderTest.PostAsync(
                "/Presale/Checkout/CreateAccount",
                new FormUrlEncodedContent(promotion),
                CancellationToken);

            if (authenticatedUnderTest)
            {
                promotionResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
                promotionResponse.RequestMessage!.RequestUri!.AbsolutePath.ShouldContain("AccessDenied");
            }
            else
            {
                promotionResponse.StatusCode.ShouldBe(HttpStatusCode.Redirect);
                promotionResponse.Headers.Location!.ToString().ShouldContain("AccessDenied");
            }

            guestDecoyToken.ShouldNotBeNullOrWhiteSpace();
            authenticatedDecoyUserId.ShouldNotBeNullOrWhiteSpace();
        }

        private async Task<HttpClient> LoginAsync(string email, string password)
        {
            var client = CreateClient();
            var token = await FetchAntiForgeryTokenAsync(client, "/Identity/Account/Login");
            var form = new Dictionary<string, string>
            {
                ["Input.Email"] = email,
                ["Input.Password"] = password,
                ["Input.RememberMe"] = "false",
                ["__RequestVerificationToken"] = token
            };
            var response = await client.PostAsync(
                "/Identity/Account/Login",
                new FormUrlEncodedContent(form),
                CancellationToken);
            response.StatusCode.ShouldBe(HttpStatusCode.OK);
            response.RequestMessage!.RequestUri!.AbsolutePath.ShouldNotContain("Login");
            return client;
        }

        private async Task<(int OrderId, int ProfileId)> PlaceAuthenticatedOrderAsync(
            HttpClient client,
            int productId,
            int profileId)
        {
            var token = await FetchAntiForgeryTokenAsync(client, "/Identity/Account/Login");
            var addForm = new Dictionary<string, string>
            {
                ["productId"] = productId.ToString(),
                ["quantity"] = "1",
                ["returnUrl"] = string.Empty,
                ["__RequestVerificationToken"] = token
            };
            await client.PostAsync(
                "/Presale/Checkout/AddToCart",
                new FormUrlEncodedContent(addForm),
                CancellationToken);
            await client.GetAsync("/Presale/Checkout/PlaceOrder", CancellationToken);

            var placeForm = new Dictionary<string, string>
            {
                ["CustomerId"] = profileId.ToString(),
                ["CurrencyId"] = "1",
                ["FirstName"] = "Auth",
                ["LastName"] = "Decoy",
                ["Email"] = UniqueEmail("isolation-auth-order"),
                ["PhoneNumber"] = "500100200",
                ["IsCompany"] = "false",
                ["Street"] = "Testowa",
                ["BuildingNumber"] = "1",
                ["ZipCode"] = "00-001",
                ["City"] = "Warszawa",
                ["Country"] = "Polska",
                ["__RequestVerificationToken"] = token
            };
            var response = await client.PostAsync(
                "/Presale/Checkout/PlaceOrder",
                new FormUrlEncodedContent(placeForm),
                CancellationToken);
            response.StatusCode.ShouldBe(HttpStatusCode.OK);
            return ParseSummaryRedirect(response.RequestMessage!.RequestUri!);
        }

        private static async Task AssertOrderDetailsDeniedAsync(
            HttpClient client,
            int orderId,
            bool authenticated)
        {
            var response = await client.GetAsync($"/Sales/Orders/Details/{orderId}", CancellationToken);
            if (authenticated)
            {
                response.StatusCode.ShouldBe(HttpStatusCode.OK);
                response.RequestMessage!.RequestUri!.AbsolutePath.ShouldContain("AccessDenied");
            }
            else
            {
                response.StatusCode.ShouldBe(HttpStatusCode.Redirect);
                response.Headers.Location!.ToString().ShouldContain("Login");
            }
        }

        private static async Task AssertSummaryDeniedAsync(
            HttpClient client,
            int orderId,
            bool authenticated)
        {
            var response = await client.GetAsync($"/Presale/Checkout/Summary/{orderId}", CancellationToken);
            if (authenticated)
            {
                response.StatusCode.ShouldBe(HttpStatusCode.OK);
                response.RequestMessage!.RequestUri!.AbsolutePath.ShouldContain("AccessDenied");
            }
            else
            {
                response.StatusCode.ShouldBe(HttpStatusCode.Redirect);
                response.Headers.Location!.ToString().ShouldContain("Login");
            }
        }
    }
}
