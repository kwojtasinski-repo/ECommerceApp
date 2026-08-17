using Shouldly;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using Xunit;

namespace ECommerceApp.Web.IntegrationTests.Presale.Checkout
{
    /// <summary>
    /// A GuestAccess ticket is documented and unit-tested (<c>OrderAccessAuthorizationHandlerTests</c>)
    /// as scoped to exactly one order — but that scoping was only ever enforced on the id-addressed
    /// resource routes (<c>Summary/{id}</c>, <c>Sales/Orders/Details/{id}</c>,
    /// <c>Sales/Payments/Create/{id}</c>). The caller-scoped list endpoints
    /// (<c>Sales/Orders/MyOrders</c>, <c>Sales/Payments/MyPayments</c>, <c>Sales/Refund/MyRefunds</c>)
    /// filtered only by <c>GetUserId()</c>, with no order-scoping at all — which surfaces a guest's
    /// earlier order once <c>ShopperIdentityResolver</c> starts resolving their id from an active
    /// GuestAccess ticket instead of minting a fresh guest cookie. These tests place two orders under
    /// the same guest session (mirroring the identity-reuse already proven by
    /// <see cref="GuestCheckoutIntegrationTests.GuestCheckout_ResubmissionForSameGuestCookie_ReusesSameProfile_NoDuplicate"/>)
    /// and assert each list now shows only the order the current ticket is scoped to.
    /// </summary>
    public sealed class GuestSingleOrderBlastRadiusTests
        : GuestCheckoutTestBase, IClassFixture<GuestCheckoutTestFactory>
    {
        public GuestSingleOrderBlastRadiusTests(GuestCheckoutTestFactory factory, ITestOutputHelper output)
            : base(factory, output) { }

        [Fact]
        public async Task MyOrders_GuestWithTwoOrdersInSameSession_ShowsOnlyCurrentlyScopedOrder()
        {
            var (client, orderId1, orderId2) = await PlaceTwoGuestOrdersInSameSessionAsync("my-orders");

            var response = await client.GetAsync("/Sales/Orders/MyOrders", CancellationToken);
            response.StatusCode.ShouldBe(HttpStatusCode.OK);
            var html = await response.Content.ReadAsStringAsync(CancellationToken);

            html.ShouldContain(RowMarker(orderId2), customMessage: "the order the current GuestAccess ticket is scoped to must still be listed");
            html.ShouldNotContain(RowMarker(orderId1), customMessage: "an earlier order from the same guest session must not leak through MyOrders once the ticket has moved on to a second order");
        }

        [Fact]
        public async Task MyPayments_GuestWithTwoOrdersInSameSession_ShowsOnlyCurrentlyScopedOrder()
        {
            var (client, orderId1, orderId2) = await PlaceTwoGuestOrdersInSameSessionAsync("my-payments");
            var userId = await _factory.GetOrderUserIdAsync(orderId2); // same identity backs both orders
            await _factory.CreatePendingPaymentAsync(orderId1, userId);
            await _factory.CreatePendingPaymentAsync(orderId2, userId);

            var response = await client.GetAsync("/Sales/Payments/MyPayments", CancellationToken);
            response.StatusCode.ShouldBe(HttpStatusCode.OK);
            var html = await response.Content.ReadAsStringAsync(CancellationToken);

            html.ShouldContain(RowMarker(orderId2));
            html.ShouldNotContain(RowMarker(orderId1),
                customMessage: "a payment for an earlier order in the same guest session must not leak through MyPayments");
        }

        [Fact]
        public async Task MyRefunds_GuestWithTwoOrdersInSameSession_ShowsOnlyCurrentlyScopedOrder()
        {
            var productId = await _factory.CreateAvailableProductAsync();
            var (client, orderId1, orderId2) = await PlaceTwoGuestOrdersInSameSessionAsync("my-refunds", productId);
            var userId = await _factory.GetOrderUserIdAsync(orderId2); // same identity backs both orders
            await _factory.CreateRefundRequestAsync(orderId1, productId, userId);
            await _factory.CreateRefundRequestAsync(orderId2, productId, userId);

            var response = await client.GetAsync("/Sales/Refund/MyRefunds", CancellationToken);
            response.StatusCode.ShouldBe(HttpStatusCode.OK);
            var html = await response.Content.ReadAsStringAsync(CancellationToken);

            html.ShouldContain(RowMarker(orderId2));
            html.ShouldNotContain(RowMarker(orderId1),
                customMessage: "a refund for an earlier order in the same guest session must not leak through MyRefunds");
        }

        /// <summary>
        /// Regression: a real registered account is never GuestAccess-scoped
        /// (<see cref="Areas.Presale.Authorization.GuestAccessScope.GetScopedOrderId"/> returns null for
        /// <c>Identity.Application</c>), so the new scoping must not narrow what a genuine customer sees
        /// across their own separate orders.
        /// </summary>
        [Fact]
        public async Task MyOrders_RegisteredCustomerWithTwoOrders_StillSeesBoth()
        {
            var client = await CreateAuthenticatedClientAsync();
            var profileId = await _factory.EnsureProfileForRegisteredUserAsync(AdminUserId, UniqueEmail("blast-radius-regression"));
            var productA = await _factory.CreateAvailableProductAsync();
            var productB = await _factory.CreateAvailableProductAsync();

            var orderId1 = await PlaceAuthenticatedOrderAsync(client, productA, profileId);
            var orderId2 = await PlaceAuthenticatedOrderAsync(client, productB, profileId);

            var response = await client.GetAsync("/Sales/Orders/MyOrders", CancellationToken);
            response.StatusCode.ShouldBe(HttpStatusCode.OK);
            var html = await response.Content.ReadAsStringAsync(CancellationToken);

            html.ShouldContain(RowMarker(orderId1));
            html.ShouldContain(RowMarker(orderId2));
        }

        [Fact]
        public async Task ConfirmOrderAccess_ExceedingPerOrderLimit_Returns429WithRetryAfter()
        {
            var client = CreateClient();
            var productId = await _factory.CreateAvailableProductAsync();
            await MintGuestCookieAsync(client);
            var afToken = await FetchAntiForgeryTokenAsync(client, AnonymousTokenSourceUrl);
            var (orderId, _) = await PlaceGuestOrderAsync(client, afToken, productId, UniqueEmail("confirm-rate-limit"));

            var requestClient = CreateClient(allowAutoRedirect: false);
            var requestToken = await FetchAntiForgeryTokenAsync(requestClient, AnonymousTokenSourceUrl);

            for (var attempt = 1; attempt <= 5; attempt++)
            {
                var form = new Dictionary<string, string>
                {
                    ["code"] = "wrong-code",
                    ["__RequestVerificationToken"] = requestToken
                };
                var response = await requestClient.PostAsync(
                    $"/Presale/Checkout/ConfirmOrderAccess/{orderId}", new FormUrlEncodedContent(form), CancellationToken);
                response.StatusCode.ShouldBe(HttpStatusCode.Redirect,
                    $"attempt {attempt} is within the 5-per-15-minute per-order limit and should be rejected as a bad code, not throttled");
            }

            var sixthForm = new Dictionary<string, string>
            {
                ["code"] = "wrong-code",
                ["__RequestVerificationToken"] = requestToken
            };
            var throttled = await requestClient.PostAsync(
                $"/Presale/Checkout/ConfirmOrderAccess/{orderId}", new FormUrlEncodedContent(sixthForm), CancellationToken);

            throttled.StatusCode.ShouldBe((HttpStatusCode)429, "the 6th redemption attempt for the same order must be throttled");
            throttled.Headers.RetryAfter.ShouldNotBeNull("a throttled response must advertise Retry-After");
        }

        /// <summary>
        /// Places a first guest order (minting the GuestAccess sign-in), then re-uses the same
        /// authenticated client to place a second order — exactly the identity-reuse path
        /// <see cref="GuestCheckoutIntegrationTests.GuestCheckout_ResubmissionForSameGuestCookie_ReusesSameProfile_NoDuplicate"/>
        /// already proves lands on the same UserProfile. Returns the client (now GuestAccess-signed-in
        /// and scoped to the second order) plus both order ids.
        /// </summary>
        private async Task<(HttpClient Client, int OrderId1, int OrderId2)> PlaceTwoGuestOrdersInSameSessionAsync(
            string emailPrefix, int? sharedProductId = null)
        {
            var client = CreateClient();
            var productId = sharedProductId ?? await _factory.CreateAvailableProductAsync();
            await MintGuestCookieAsync(client);
            var afToken = await FetchAntiForgeryTokenAsync(client, AnonymousTokenSourceUrl);

            var (orderId1, profileId1) = await PlaceGuestOrderAsync(
                client, afToken, productId, UniqueEmail($"{emailPrefix}-first"));

            // PlaceOrder POST above minted a GuestAccess sign-in — the antiforgery token embeds the
            // (then-anonymous) identity and is stale now, same reason
            // GuestCheckout_ResubmissionForSameGuestCookie_ReusesSameProfile_NoDuplicate re-fetches one.
            var afTokenAfterSignIn = await FetchAntiForgeryTokenAsync(
                client, $"/Presale/Checkout/Summary/{orderId1}?profileId={profileId1}&guest=True");
            var (orderId2, _) = await PlaceGuestOrderAsync(
                client, afTokenAfterSignIn, productId, UniqueEmail($"{emailPrefix}-second"));

            return (client, orderId1, orderId2);
        }

        private async Task<int> PlaceAuthenticatedOrderAsync(HttpClient client, int productId, int profileId)
        {
            var token = await FetchAntiForgeryTokenAsync(client, "/Identity/Account/Login");
            var addForm = new Dictionary<string, string>
            {
                ["productId"] = productId.ToString(),
                ["quantity"] = "1",
                ["returnUrl"] = string.Empty,
                ["__RequestVerificationToken"] = token
            };
            await client.PostAsync("/Presale/Checkout/AddToCart", new FormUrlEncodedContent(addForm), CancellationToken);
            await client.GetAsync("/Presale/Checkout/PlaceOrder", CancellationToken);

            var placeForm = new Dictionary<string, string>
            {
                ["CustomerId"] = profileId.ToString(),
                ["CurrencyId"] = "1",
                ["FirstName"] = "Blast",
                ["LastName"] = "Radius",
                ["Email"] = UniqueEmail("blast-radius-order"),
                ["PhoneNumber"] = "500100200",
                ["IsCompany"] = "false",
                ["Street"] = "Testowa",
                ["BuildingNumber"] = "1",
                ["ZipCode"] = "00-001",
                ["City"] = "Warszawa",
                ["Country"] = "Polska",
                ["__RequestVerificationToken"] = token
            };
            var response = await client.PostAsync("/Presale/Checkout/PlaceOrder", new FormUrlEncodedContent(placeForm), CancellationToken);
            response.StatusCode.ShouldBe(HttpStatusCode.OK);
            var (orderId, _) = ParseSummaryRedirect(response.RequestMessage!.RequestUri!);
            return orderId;
        }

        private static string RowMarker(int orderId) => $">{orderId}<";
    }
}
