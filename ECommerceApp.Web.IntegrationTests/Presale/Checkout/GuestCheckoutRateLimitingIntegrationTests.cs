using Shouldly;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using Xunit;

namespace ECommerceApp.Web.IntegrationTests.Presale.Checkout
{
    /// <summary>
    /// Per-IP rate limiting on the two guest-checkout actions that grow DB footprint for an anonymous
    /// caller (Startup.cs "GuestCheckoutAddToCart"/"GuestCheckoutPlaceOrder" policies) — the gap flagged
    /// after ADR-0030 Phase 9's own rate limiting only covered RequestOrderAccess. In its own test class
    /// (own <see cref="GuestCheckoutTestFactory"/> instance, own rate-limiter state) deliberately, so
    /// exhausting these limits cannot bleed into or be polluted by AddToCart/PlaceOrder calls made by
    /// unrelated tests sharing a factory.
    /// </summary>
    public class GuestCheckoutRateLimitingIntegrationTests
        : GuestCheckoutTestBase, IClassFixture<GuestCheckoutTestFactory>
    {
        public GuestCheckoutRateLimitingIntegrationTests(GuestCheckoutTestFactory factory, ITestOutputHelper output)
            : base(factory, output) { }

        [Fact]
        public async Task AddToCart_ExceedingPerIpLimit_Returns429WithRetryAfter()
        {
            var client = CreateClient(allowAutoRedirect: false);
            await MintGuestCookieAsync(client);
            var afToken = await FetchAntiForgeryTokenAsync(client, AnonymousTokenSourceUrl);

            for (var attempt = 1; attempt <= 30; attempt++)
            {
                var productId = await _factory.CreateAvailableProductAsync();
                var form = new Dictionary<string, string>
                {
                    ["productId"] = productId.ToString(),
                    ["quantity"] = "1",
                    ["returnUrl"] = "",
                    ["__RequestVerificationToken"] = afToken
                };
                var response = await client.PostAsync(
                    "/Presale/Checkout/AddToCart", new FormUrlEncodedContent(form), CancellationToken);
                response.StatusCode.ShouldNotBe((HttpStatusCode)429,
                    $"attempt {attempt} is within the 30-per-10-minute per-IP limit and should not be throttled");
            }

            var throttledProductId = await _factory.CreateAvailableProductAsync();
            var throttledForm = new Dictionary<string, string>
            {
                ["productId"] = throttledProductId.ToString(),
                ["quantity"] = "1",
                ["returnUrl"] = "",
                ["__RequestVerificationToken"] = afToken
            };
            var throttled = await client.PostAsync(
                "/Presale/Checkout/AddToCart", new FormUrlEncodedContent(throttledForm), CancellationToken);

            throttled.StatusCode.ShouldBe((HttpStatusCode)429, "the 31st AddToCart in the window must be throttled");
            throttled.Headers.RetryAfter.ShouldNotBeNull("a throttled response must advertise Retry-After");
        }

        [Fact]
        public async Task PlaceOrder_ExceedingPerIpLimit_Returns429WithRetryAfter()
        {
            var client = CreateClient(allowAutoRedirect: false);
            await MintGuestCookieAsync(client);
            var afToken = await FetchAntiForgeryTokenAsync(client, AnonymousTokenSourceUrl);

            // Deliberately never AddToCart first, so every PlaceOrder attempt hits NoSoftReservations and
            // redirects back to Cart instead of succeeding — the caller stays anonymous throughout, so
            // the single antiforgery token fetched above (before any GuestAccess sign-in could occur)
            // stays valid for every attempt in this loop.
            var form = new Dictionary<string, string>
            {
                ["CurrencyId"] = "1",
                ["FirstName"] = "Jan",
                ["LastName"] = "Kowalski",
                ["Email"] = UniqueEmail("rate-limit-place-order"),
                ["PhoneNumber"] = "500100200",
                ["IsCompany"] = "false",
                ["Street"] = "Testowa",
                ["BuildingNumber"] = "1",
                ["ZipCode"] = "00-001",
                ["City"] = "Warszawa",
                ["Country"] = "Polska",
                ["__RequestVerificationToken"] = afToken
            };

            for (var attempt = 1; attempt <= 10; attempt++)
            {
                var response = await client.PostAsync(
                    "/Presale/Checkout/PlaceOrder", new FormUrlEncodedContent(form), CancellationToken);
                response.StatusCode.ShouldNotBe((HttpStatusCode)429,
                    $"attempt {attempt} is within the 10-per-10-minute per-IP limit and should not be throttled");
            }

            var throttled = await client.PostAsync(
                "/Presale/Checkout/PlaceOrder", new FormUrlEncodedContent(form), CancellationToken);

            throttled.StatusCode.ShouldBe((HttpStatusCode)429, "the 11th PlaceOrder in the window must be throttled");
            throttled.Headers.RetryAfter.ShouldNotBeNull("a throttled response must advertise Retry-After");
        }
    }
}
