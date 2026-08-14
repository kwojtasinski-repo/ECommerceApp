using ECommerceApp.Web.Areas.Presale;
using Shouldly;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using Xunit;

namespace ECommerceApp.Web.IntegrationTests.Presale.Checkout
{
    /// <summary>
    /// ADR-0030 Phase 1 — Guest shopper identity, exercised through the real ASP.NET Core HTTP
    /// pipeline (routing, authorization middleware, model binding, antiforgery), not just against a
    /// hand-built <c>DefaultHttpContext</c> as <c>ShopperIdentityResolverTests</c> does.
    ///
    /// Closes the gap recorded in
    /// <c>.github/plans/01-phase-guest-shopper-identity-validation.md</c> Findings #1: none of the
    /// checklist's four HTTP-level assertions (anonymous Cart GET + Set-Cookie, cookie reused across
    /// requests, anonymous AddToCart, anonymous PlaceOrder GET) previously existed anywhere in the repo.
    /// </summary>
    public class GuestCartIntegrationTests
        : GuestCheckoutTestBase, IClassFixture<GuestCheckoutTestFactory>
    {
        public GuestCartIntegrationTests(GuestCheckoutTestFactory factory, ITestOutputHelper output)
            : base(factory, output) { }

        [Fact]
        public async Task Cart_Anonymous_ReturnsOk_NoLoginRedirect()
        {
            var client = CreateClient();

            var response = await client.GetAsync("/Presale/Checkout/Cart", CancellationToken);

            response.StatusCode.ShouldBe(HttpStatusCode.OK);
            response.RequestMessage!.RequestUri!.AbsolutePath.ShouldNotContain("Login", Case.Insensitive);
        }

        [Fact]
        public async Task Cart_Anonymous_SetsGuestCookie_HttpOnlySecureSameSiteLaxAndPrefixed()
        {
            var client = CreateClient();

            var response = await client.GetAsync("/Presale/Checkout/Cart", CancellationToken);

            var rawCookie = ExtractRawSetCookieHeader(response, GuestSession.CookieName);
            rawCookie.ShouldNotBeNull($"expected a Set-Cookie header for '{GuestSession.CookieName}'");

            var value = ExtractCookieValue(response, GuestSession.CookieName);
            // Prefixed so it can never collide with an AspNetUsers.Id by construction (ADR-0030 §1a).
            value.ShouldStartWith("gst_");

            rawCookie.ShouldContain("httponly", Case.Insensitive);
            rawCookie.ShouldContain("secure", Case.Insensitive);
            rawCookie.ShouldContain("samesite=lax", Case.Insensitive);
        }

        [Fact]
        public async Task Cart_Anonymous_SecondRequestWithSameCookie_SeesSameCartIdentity()
        {
            var client = CreateClient(); // HandleCookies=true -> the same guest cookie is resent automatically
            var productId = await _factory.CreateAvailableProductAsync();

            await MintGuestCookieAsync(client);
            var token = await FetchAntiForgeryTokenAsync(client, AnonymousTokenSourceUrl);

            var addForm = new Dictionary<string, string>
            {
                ["productId"] = productId.ToString(),
                ["quantity"] = "1",
                ["returnUrl"] = "",
                ["__RequestVerificationToken"] = token
            };
            var addResponse = await client.PostAsync("/Presale/Checkout/AddToCart", new FormUrlEncodedContent(addForm), CancellationToken);
            addResponse.StatusCode.ShouldBe(HttpStatusCode.OK);

            // A second, independent request reusing the same cookie jar must resolve to the same
            // PresaleUserId and therefore see the item that was just added.
            var cartResponse = await client.GetAsync("/Presale/Checkout/Cart", CancellationToken);
            var html = await cartResponse.Content.ReadAsStringAsync(CancellationToken);

            // Proves the second request resolved to the same guest identity as the first.
            html.ShouldContain($"/offers/{productId}");
        }

        [Fact]
        public async Task AddToCart_Anonymous_Succeeds_NoLoginRedirect()
        {
            var client = CreateClient();
            var productId = await _factory.CreateAvailableProductAsync();
            var token = await FetchAntiForgeryTokenAsync(client, AnonymousTokenSourceUrl);

            var form = new Dictionary<string, string>
            {
                ["productId"] = productId.ToString(),
                ["quantity"] = "1",
                ["returnUrl"] = "",
                ["__RequestVerificationToken"] = token
            };
            var response = await client.PostAsync("/Presale/Checkout/AddToCart", new FormUrlEncodedContent(form), CancellationToken);

            response.StatusCode.ShouldBe(HttpStatusCode.OK);
            response.RequestMessage!.RequestUri!.AbsolutePath.ShouldNotContain("Login", Case.Insensitive);
        }

        [Fact]
        public async Task PlaceOrder_Get_Anonymous_ReturnsOk_NoLoginRedirect()
        {
            var client = CreateClient();

            // Empty cart -> the action redirects to Cart, not to /Account/Login. Either way, no auth
            // challenge must occur for an anonymous caller.
            var response = await client.GetAsync("/Presale/Checkout/PlaceOrder", CancellationToken);

            response.StatusCode.ShouldBe(HttpStatusCode.OK);
            response.RequestMessage!.RequestUri!.AbsolutePath.ShouldNotContain("Login", Case.Insensitive);
        }

        [Fact]
        public async Task GuestBrowsing_NeverCreatesApplicationUserOrIdentityAuthCookie()
        {
            var client = CreateClient();
            var productId = await _factory.CreateAvailableProductAsync();

            var guestToken = await MintGuestCookieAsync(client);
            var afToken = await FetchAntiForgeryTokenAsync(client, AnonymousTokenSourceUrl);

            var addForm = new Dictionary<string, string>
            {
                ["productId"] = productId.ToString(),
                ["quantity"] = "1",
                ["returnUrl"] = "",
                ["__RequestVerificationToken"] = afToken
            };
            var addResponse = await client.PostAsync("/Presale/Checkout/AddToCart", new FormUrlEncodedContent(addForm), CancellationToken);
            var placeGetResponse = await client.GetAsync("/Presale/Checkout/PlaceOrder", CancellationToken);

            foreach (var response in new[] { addResponse, placeGetResponse })
            {
                if (response.Headers.TryGetValues("Set-Cookie", out var cookies))
                {
                    foreach (var cookie in cookies)
                    {
                        // Guest browsing must never issue an Identity authentication cookie.
                        cookie.ShouldNotContain(".AspNetCore.Identity");
                    }
                }
            }

            // Phase 1 (identity resolution only) must not create an AccountProfile row — that only
            // happens at PlaceOrder POST time (Phase 2's EnsureGuestCustomerAsync).
            var profile = await _factory.FindProfileByUserIdAsync(guestToken);
            profile.ShouldBeNull();
        }
    }
}
