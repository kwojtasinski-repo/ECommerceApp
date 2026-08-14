using ECommerceApp.Web.Areas.Presale;
using Microsoft.AspNetCore.Mvc.Testing;
using Shouldly;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using Xunit;

namespace ECommerceApp.Web.IntegrationTests.Presale.Checkout
{
    /// <summary>
    /// Shared helpers for the ADR-0030 guest-checkout HTTP integration tests. All three phase test
    /// classes (<see cref="GuestCartIntegrationTests"/>, <see cref="GuestCheckoutIntegrationTests"/>,
    /// <see cref="GuestPromotionIntegrationTests"/>) derive from this instead of duplicating the
    /// anonymous-request plumbing (guest cookie extraction, antiforgery token fetch, the full
    /// AddToCart → PlaceOrder GET → PlaceOrder POST flow, redirect-query parsing).
    ///
    /// <see cref="GuestSession"/> is <c>internal</c> in <c>ECommerceApp.Web</c>, reachable here via
    /// the <c>InternalsVisibleTo("ECommerceApp.Web.IntegrationTests")</c> declared on that assembly —
    /// used so the cookie name asserted against is the real production constant, not a hardcoded copy.
    /// </summary>
    public abstract class GuestCheckoutTestBase : WebTestBase<GuestCheckoutTestFactory>
    {
        /// <summary>Any anonymous, always-reachable page that renders a <c>__RequestVerificationToken</c>
        /// hidden field. Antiforgery tokens in this app are not page/action-scoped (no custom
        /// <c>AdditionalDataProvider</c> is configured), so a token fetched here validates against any
        /// <c>[ValidateAntiForgeryToken]</c> action for the same client/cookie session.</summary>
        protected const string AnonymousTokenSourceUrl = "/Identity/Account/Login";

        /// <summary>Seeded by <c>ECommerceApp.Shared.TestInfrastructure.Utilities.InitializeIamUsers</c> —
        /// the same ApplicationUser <see cref="WebTestBase{TFactory}.CreateAuthenticatedClientAsync"/> logs
        /// in as (<see cref="AdminEmail"/>/<see cref="AdminPassword"/>).</summary>
        protected const string AdminUserId = "a85e6eb8-242d-4bbe-9ce6-b2fbb2ddbb4e";

        protected GuestCheckoutTestBase(GuestCheckoutTestFactory factory, ITestOutputHelper output)
            : base(factory, output) { }

        /// <summary>
        /// Shadows <see cref="WebTestBase{TFactory}.CreateClient"/> with an <c>https://</c> base
        /// address. <see cref="GuestSession.CookieOptions"/> marks the guest cookie <c>Secure</c> (per
        /// ADR-0030 §1a — correct, production-faithful behavior). The base helper's client defaults to
        /// <c>http://localhost</c>, over which .NET's cookie container will store but never re-attach a
        /// Secure cookie on subsequent requests — every "anonymous request" would silently mint a brand
        /// new guest identity instead of reusing the one from the previous request in the same test.
        /// TestServer has no real TLS binding, so it honors whatever scheme the request URI carries;
        /// using <c>https://localhost</c> here costs nothing and makes Secure-cookie round-tripping work
        /// exactly like a real browser session would.
        /// </summary>
        protected new HttpClient CreateClient() => CreateClient(allowAutoRedirect: true);

        /// <summary>Overload that can disable auto-redirect-following, needed to observe the raw
        /// pre-redirect response for actions that call <c>Forbid()</c> — see
        /// <see cref="GuestPromotionIntegrationTests.Promotion_DifferentGuestSession_CannotPromoteAnothersProfile_Returns403AndDoesNotMutate"/>.</summary>
        protected HttpClient CreateClient(bool allowAutoRedirect)
            => _factory.CreateClient(new WebApplicationFactoryClientOptions
            {
                AllowAutoRedirect = allowAutoRedirect,
                HandleCookies = true,
                BaseAddress = new Uri("https://localhost")
            });

        /// <summary>GETs the Cart page (first anonymous Checkout action) to mint the guest cookie
        /// deterministically, and returns the raw guest token value (e.g. <c>gst_...</c>).</summary>
        protected async Task<string> MintGuestCookieAsync(HttpClient client)
        {
            var response = await client.GetAsync("/Presale/Checkout/Cart", CancellationToken);
            response.StatusCode.ShouldBe(HttpStatusCode.OK, "anonymous GET /Presale/Checkout/Cart must succeed without a login redirect");

            var token = ExtractCookieValue(response, GuestSession.CookieName);
            token.ShouldNotBeNullOrEmpty("expected ShopperIdentityResolver to mint a guest cookie on the first anonymous Checkout request");
            return token;
        }

        /// <summary>Extracts a specific cookie's value from a response's raw <c>Set-Cookie</c> headers.</summary>
        protected static string ExtractCookieValue(HttpResponseMessage response, string cookieName)
        {
            var raw = ExtractRawSetCookieHeader(response, cookieName);
            if (raw is null) return null;

            var start = cookieName.Length + 1; // past "name="
            var end = raw.IndexOf(';', start);
            return end > start ? raw[start..end] : raw[start..];
        }

        /// <summary>Returns the full raw <c>Set-Cookie</c> header (name=value; attributes...) for the
        /// given cookie name, or null if the response didn't set it.</summary>
        protected static string ExtractRawSetCookieHeader(HttpResponseMessage response, string cookieName)
        {
            if (!response.Headers.TryGetValues("Set-Cookie", out var cookies))
                return null;

            return cookies.FirstOrDefault(c => c.StartsWith(cookieName + "=", StringComparison.Ordinal));
        }

        /// <summary>Runs the full anonymous AddToCart → PlaceOrder(GET) → PlaceOrder(POST) flow for a
        /// single product and returns the placed order's id and the resolved guest UserProfile id
        /// (parsed from the <c>Summary</c> redirect's query string, which the controller always
        /// populates from its own server-side <c>EnsureGuestCustomerAsync</c> result — never from the
        /// client-supplied <c>CustomerId</c>).</summary>
        protected async Task<(int OrderId, int ProfileId)> PlaceGuestOrderAsync(
            HttpClient client,
            string antiForgeryToken,
            int productId,
            string email,
            int? customerIdOverride = null,
            int quantity = 1)
        {
            var addForm = new Dictionary<string, string>
            {
                ["productId"] = productId.ToString(),
                ["quantity"] = quantity.ToString(),
                ["returnUrl"] = "",
                ["__RequestVerificationToken"] = antiForgeryToken
            };
            var addResponse = await client.PostAsync("/Presale/Checkout/AddToCart", new FormUrlEncodedContent(addForm), CancellationToken);
            addResponse.StatusCode.ShouldBe(HttpStatusCode.OK, "AddToCart should succeed and redirect (followed) to the Cart page");

            var placeGet = await client.GetAsync("/Presale/Checkout/PlaceOrder", CancellationToken);
            placeGet.StatusCode.ShouldBe(HttpStatusCode.OK);
            placeGet.RequestMessage!.RequestUri!.AbsolutePath.ShouldBe(
                "/Presale/Checkout/PlaceOrder",
                "expected the soft reservation to succeed and render the PlaceOrder form, not redirect back to Cart");

            var placeForm = new Dictionary<string, string>
            {
                ["CurrencyId"] = "1",
                ["FirstName"] = "Jan",
                ["LastName"] = "Kowalski",
                ["Email"] = email,
                ["PhoneNumber"] = "500100200",
                ["IsCompany"] = "false",
                ["Street"] = "Testowa",
                ["BuildingNumber"] = "1",
                ["ZipCode"] = "00-001",
                ["City"] = "Warszawa",
                ["Country"] = "Polska",
                ["__RequestVerificationToken"] = antiForgeryToken
            };
            if (customerIdOverride.HasValue)
            {
                placeForm["CustomerId"] = customerIdOverride.Value.ToString();
            }

            var placePost = await client.PostAsync("/Presale/Checkout/PlaceOrder", new FormUrlEncodedContent(placeForm), CancellationToken);
            placePost.StatusCode.ShouldBe(HttpStatusCode.OK);
            placePost.RequestMessage!.RequestUri!.AbsolutePath.ShouldContain(
                "Summary", Case.Insensitive, "expected a successful guest order to redirect to the Summary page");

            return ParseSummaryRedirect(placePost.RequestMessage.RequestUri);
        }

        /// <summary>
        /// Parses the (orderId, profileId) pair out of a <c>Summary</c> redirect URL. The "areas" route
        /// (<c>{area:exists}/{controller}/{action=Index}/{id?}</c>) places <c>id</c> (the orderId) in the
        /// URL path, while <c>profileId</c>/<c>guest</c> — not part of the route template — end up in the
        /// query string.
        /// </summary>
        protected static (int OrderId, int ProfileId) ParseSummaryRedirect(Uri summaryUri)
        {
            var segments = summaryUri.AbsolutePath.Split('/', StringSplitOptions.RemoveEmptyEntries);
            var orderId = int.Parse(segments[^1]);

            var query = ParseQuery(summaryUri);
            var profileId = int.Parse(query["profileId"]);

            return (orderId, profileId);
        }

        protected static Dictionary<string, string> ParseQuery(Uri uri)
        {
            var result = new Dictionary<string, string>();
            var query = uri.Query.TrimStart('?');
            if (string.IsNullOrEmpty(query)) return result;

            foreach (var pair in query.Split('&', StringSplitOptions.RemoveEmptyEntries))
            {
                var parts = pair.Split('=', 2);
                result[Uri.UnescapeDataString(parts[0])] = parts.Length > 1 ? Uri.UnescapeDataString(parts[1]) : string.Empty;
            }
            return result;
        }

        protected static string UniqueEmail(string prefix) => $"{prefix}-{Guid.NewGuid():N}@guest-checkout.test";
    }
}
