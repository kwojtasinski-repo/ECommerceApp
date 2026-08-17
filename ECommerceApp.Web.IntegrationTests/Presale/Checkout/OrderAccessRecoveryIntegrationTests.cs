using ECommerceApp.Application.Presale.Checkout.Contracts;
using ECommerceApp.Web.Areas.Presale.Authorization;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using Xunit;

namespace ECommerceApp.Web.IntegrationTests.Presale.Checkout
{
    public class OrderAccessRecoveryIntegrationTests
        : GuestCheckoutTestBase, IClassFixture<GuestCheckoutTestFactory>
    {
        public OrderAccessRecoveryIntegrationTests(GuestCheckoutTestFactory factory, ITestOutputHelper output)
            : base(factory, output) { }

        [Fact]
        public async Task GuestOrder_SetsGuestAccessCookie_AndFreshClientCanOpenSummary()
        {
            var placingClient = CreateClient(allowAutoRedirect: false);
            var productId = await _factory.CreateAvailableProductAsync();
            await MintGuestCookieAsync(placingClient);
            var antiForgeryToken = await FetchAntiForgeryTokenAsync(placingClient, AnonymousTokenSourceUrl);
            var email = UniqueEmail("order-access");

            var addForm = new Dictionary<string, string>
            {
                ["productId"] = productId.ToString(),
                ["quantity"] = "1",
                ["returnUrl"] = "",
                ["__RequestVerificationToken"] = antiForgeryToken
            };
            await placingClient.PostAsync("/Presale/Checkout/AddToCart", new FormUrlEncodedContent(addForm), CancellationToken);
            await placingClient.GetAsync("/Presale/Checkout/PlaceOrder", CancellationToken);

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
            var placeResponse = await placingClient.PostAsync(
                "/Presale/Checkout/PlaceOrder",
                new FormUrlEncodedContent(placeForm),
                CancellationToken);

            placeResponse.StatusCode.ShouldBe(HttpStatusCode.Redirect);
            var summaryUri = new Uri(new Uri("https://localhost"), placeResponse.Headers.Location!.OriginalString);
            var orderId = int.Parse(summaryUri.AbsolutePath.Split('/', System.StringSplitOptions.RemoveEmptyEntries)[^1]);
            var guestAccessCookie = ExtractCookieValue(placeResponse, GuestAccessDefaults.CookieName);
            guestAccessCookie.ShouldNotBeNullOrWhiteSpace();

            var freshClient = CreateClient(allowAutoRedirect: false);
            freshClient.DefaultRequestHeaders.Add("Cookie", $"{GuestAccessDefaults.CookieName}={guestAccessCookie}");
            var summary = await freshClient.GetAsync($"/Presale/Checkout/Summary/{orderId}", CancellationToken);

            summary.StatusCode.ShouldBe(HttpStatusCode.OK);
        }

        [Fact]
        public async Task SummaryForMissingOrder_ReturnsNotFound()
        {
            var client = CreateClient(allowAutoRedirect: false);

            var response = await client.GetAsync("/Presale/Checkout/Summary/999999", CancellationToken);

            response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        }

        [Fact]
        public async Task PaymentsCreate_AnonymousWithGuestAccess_AllowsOwnOrder_RejectsOtherOrder()
        {
            var productId = await _factory.CreateAvailableProductAsync();

            var clientA = CreateClient();
            var guestTokenA = await MintGuestCookieAsync(clientA);
            var tokenA = await FetchAntiForgeryTokenAsync(clientA, AnonymousTokenSourceUrl);
            var (orderIdA, _) = await PlaceGuestOrderAsync(clientA, tokenA, productId, UniqueEmail("payments-own"));
            await _factory.CreatePendingPaymentAsync(orderIdA, guestTokenA);

            var ownResponse = await clientA.GetAsync($"/Sales/Payments/Create/{orderIdA}", CancellationToken);
            ownResponse.StatusCode.ShouldBe(HttpStatusCode.OK,
                "the order-access cookie should authorize the anonymous caller for their own order's payment page");

            var clientB = CreateClient();
            var guestTokenB = await MintGuestCookieAsync(clientB);
            var tokenB = await FetchAntiForgeryTokenAsync(clientB, AnonymousTokenSourceUrl);
            var (orderIdB, _) = await PlaceGuestOrderAsync(clientB, tokenB, productId, UniqueEmail("payments-other"));
            await _factory.CreatePendingPaymentAsync(orderIdB, guestTokenB);

            var otherResponse = await clientA.GetAsync($"/Sales/Payments/Create/{orderIdB}", CancellationToken);
            otherResponse.RequestMessage!.RequestUri!.AbsolutePath.ShouldContain(
                "/Presale/Checkout/Order/", Case.Insensitive,
                "clientA's order-access cookie is scoped to order A and must not unlock order B's payment page; " +
                "rejection routes to the unified order-lookup path, not a dead-end Access Denied page");
        }

        /// <summary>
        /// Regression: the GET to <c>PaymentsController.Create</c> was correctly opened up to anonymous
        /// order-access-cookie holders, but the POST overload that actually calls
        /// <c>IPaymentService.ConfirmAsync</c> had no <c>[AllowAnonymous]</c> override and inherited the
        /// controller's class-level <c>[Authorize]</c> — so a guest could see the payment form but any
        /// attempt to submit it silently bounced to <c>/Identity/Account/Login</c> and the payment was
        /// never confirmed. Found during Phase 8 validation; fixed directly in
        /// <c>ECommerceApp.Web/Areas/Sales/Controllers/PaymentsController.cs</c>'s POST <c>Create</c>.
        /// </summary>
        [Fact]
        public async Task PaymentsCreate_Post_AnonymousWithGuestAccess_ActuallyConfirmsOwnPayment_RejectsOtherOrder()
        {
            var productId = await _factory.CreateAvailableProductAsync();

            var clientA = CreateClient();
            var guestTokenA = await MintGuestCookieAsync(clientA);
            var formTokenA = await FetchAntiForgeryTokenAsync(clientA, AnonymousTokenSourceUrl);
            var (orderIdA, _) = await PlaceGuestOrderAsync(clientA, formTokenA, productId, UniqueEmail("payments-post-own"));
            var paymentIdA = await _factory.CreatePendingPaymentAsync(orderIdA, guestTokenA);

            // PlaceGuestOrderAsync's PlaceOrder POST mints the GuestAccess sign-in, so the caller's
            // identity changes from anonymous to authenticated mid-flow. ASP.NET Core's antiforgery
            // token embeds the identity active at generation time, so the token fetched above (while
            // still anonymous) is no longer valid for a request made after that sign-in — a real browser
            // never hits this because the payment page's own GET always renders a fresh, already-
            // authenticated token. Fetch a fresh one the same way here.
            var ownFormToken = await FetchAntiForgeryTokenAsync(clientA, $"/Sales/Payments/Create/{orderIdA}");
            var confirmOwn = new Dictionary<string, string>
            {
                ["PaymentId"] = paymentIdA.ToString(),
                ["TransactionRef"] = System.Guid.NewGuid().ToString(),
                ["__RequestVerificationToken"] = ownFormToken
            };
            var ownResponse = await clientA.PostAsync(
                "/Sales/Payments/Create", new FormUrlEncodedContent(confirmOwn), CancellationToken);
            ownResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
            ownResponse.RequestMessage!.RequestUri!.AbsolutePath.ShouldNotContain(
                "/Identity/Account/Login", Case.Insensitive,
                "an anonymous caller with a valid order-access cookie must be able to actually confirm their own payment, not be bounced to login");
            ownResponse.RequestMessage!.RequestUri!.AbsolutePath.ShouldContain(
                "/Sales/Orders/Details", Case.Insensitive,
                "a successful anonymous payment confirmation should land on the (already anonymous-accessible) order details page");

            var clientB = CreateClient();
            var guestTokenB = await MintGuestCookieAsync(clientB);
            var formTokenB = await FetchAntiForgeryTokenAsync(clientB, AnonymousTokenSourceUrl);
            var (orderIdB, _) = await PlaceGuestOrderAsync(clientB, formTokenB, productId, UniqueEmail("payments-post-other"));
            var paymentIdB = await _factory.CreatePendingPaymentAsync(orderIdB, guestTokenB);

            var confirmOther = new Dictionary<string, string>
            {
                ["PaymentId"] = paymentIdB.ToString(),
                ["TransactionRef"] = System.Guid.NewGuid().ToString(),
                ["__RequestVerificationToken"] = ownFormToken
            };
            var otherPostResponse = await clientA.PostAsync(
                "/Sales/Payments/Create", new FormUrlEncodedContent(confirmOther), CancellationToken);
            otherPostResponse.RequestMessage!.RequestUri!.AbsolutePath.ShouldContain(
                "/Presale/Checkout/Order/", Case.Insensitive,
                "clientA's order-access cookie is scoped to order A and must not confirm order B's payment; " +
                "rejection routes to the unified order-lookup path, not a dead-end Access Denied page");
        }

        [Fact]
        public async Task SalesOrdersDetails_AnonymousWithGuestAccess_AllowsOwnOrder_RejectsOtherOrder()
        {
            var productId = await _factory.CreateAvailableProductAsync();

            var clientA = CreateClient();
            await MintGuestCookieAsync(clientA);
            var tokenA = await FetchAntiForgeryTokenAsync(clientA, AnonymousTokenSourceUrl);
            var (orderIdA, _) = await PlaceGuestOrderAsync(clientA, tokenA, productId, UniqueEmail("orders-own"));

            var ownResponse = await clientA.GetAsync($"/Sales/Orders/Details/{orderIdA}", CancellationToken);
            ownResponse.StatusCode.ShouldBe(HttpStatusCode.OK, "the order-access cookie should authorize the anonymous caller for their own order's details page");

            var clientB = CreateClient();
            await MintGuestCookieAsync(clientB);
            var tokenB = await FetchAntiForgeryTokenAsync(clientB, AnonymousTokenSourceUrl);
            var (orderIdB, _) = await PlaceGuestOrderAsync(clientB, tokenB, productId, UniqueEmail("orders-other"));

            var otherResponse = await clientA.GetAsync($"/Sales/Orders/Details/{orderIdB}", CancellationToken);
            otherResponse.RequestMessage!.RequestUri!.AbsolutePath.ShouldContain(
                "/Presale/Checkout/Order/", Case.Insensitive,
                "clientA's order-access cookie is scoped to order A and must not unlock order B's details page; " +
                "rejection routes to the unified order-lookup path, not a dead-end Access Denied page");
        }

        /// <summary>
        /// ADR-0030 revision note / Phase 9 plan §"Precondition": rate limiting on RequestOrderAccess is a
        /// hard blocker, not optional. This is the HTTP-level proof it actually throttles (429 +
        /// Retry-After), not just that a policy is registered — Luna's own Phase 9 report flagged this
        /// test as not yet written. Exercises the per-OrderId limit (5 requests/15min, Startup.cs), which
        /// is deterministic to trigger from a single test without needing 10 distinct client IPs.
        /// </summary>
        [Fact]
        public async Task RequestOrderAccess_ExceedingPerOrderLimit_Returns429WithRetryAfter()
        {
            var client = CreateClient();
            var productId = await _factory.CreateAvailableProductAsync();
            await MintGuestCookieAsync(client);
            var afToken = await FetchAntiForgeryTokenAsync(client, AnonymousTokenSourceUrl);
            var (orderId, _) = await PlaceGuestOrderAsync(client, afToken, productId, UniqueEmail("rate-limit"));

            var requestClient = CreateClient(allowAutoRedirect: false);
            var requestToken = await FetchAntiForgeryTokenAsync(requestClient, AnonymousTokenSourceUrl);

            for (var attempt = 1; attempt <= 5; attempt++)
            {
                var form = new Dictionary<string, string>
                {
                    ["email"] = UniqueEmail("rate-limit-attempt"),
                    ["__RequestVerificationToken"] = requestToken
                };
                var response = await requestClient.PostAsync(
                    $"/Presale/Checkout/RequestOrderAccess/{orderId}", new FormUrlEncodedContent(form), CancellationToken);
                response.StatusCode.ShouldBe(HttpStatusCode.Redirect,
                    $"attempt {attempt} is within the 5-per-15-minute per-order limit and should succeed");
            }

            var sixthForm = new Dictionary<string, string>
            {
                ["email"] = UniqueEmail("rate-limit-sixth"),
                ["__RequestVerificationToken"] = requestToken
            };
            var throttled = await requestClient.PostAsync(
                $"/Presale/Checkout/RequestOrderAccess/{orderId}", new FormUrlEncodedContent(sixthForm), CancellationToken);

            throttled.StatusCode.ShouldBe((HttpStatusCode)429, "the 6th request in the window must be throttled");
            throttled.Headers.RetryAfter.ShouldNotBeNull("a throttled response must advertise Retry-After");
        }

    }
}
