using ECommerceApp.Application.Presale.Checkout.Contracts;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
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
        public async Task GuestOrder_SetsAccessCookie_AndFreshClientCanOpenSummary()
        {
            var placingClient = CreateClient();
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

            placeResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
            var summaryUri = placeResponse.RequestMessage!.RequestUri!;
            var query = ParseQuery(summaryUri);
            var token = query["token"];
            token.ShouldNotBeNullOrWhiteSpace();
            var orderId = int.Parse(summaryUri.AbsolutePath.Split('/', System.StringSplitOptions.RemoveEmptyEntries)[^1]);

            var freshClient = CreateClient(allowAutoRedirect: false);
            freshClient.DefaultRequestHeaders.Add("Cookie", $"{Areas.Presale.OrderAccessCookie.CookieName}={token}");
            var summary = await freshClient.GetAsync($"/Presale/Checkout/Summary/{orderId}", CancellationToken);

            summary.StatusCode.ShouldBe(HttpStatusCode.OK);
        }

        [Fact]
        public async Task SummaryWithoutAccessToken_RedirectsToLogin()
        {
            var client = CreateClient(allowAutoRedirect: false);

            var response = await client.GetAsync("/Presale/Checkout/Summary/999999", CancellationToken);

            response.StatusCode.ShouldBe(HttpStatusCode.Redirect);
            response.Headers.Location!.OriginalString.ShouldContain("/Identity/Account/Login");
        }

        [Fact]
        public async Task LoginWithValidGuestOrderToken_RendersRecoveryForm()
        {
            var client = CreateClient();
            var productId = await _factory.CreateAvailableProductAsync();
            await MintGuestCookieAsync(client);
            var antiForgeryToken = await FetchAntiForgeryTokenAsync(client, AnonymousTokenSourceUrl);
            var addForm = new Dictionary<string, string>
            {
                ["productId"] = productId.ToString(),
                ["quantity"] = "1",
                ["returnUrl"] = "",
                ["__RequestVerificationToken"] = antiForgeryToken
            };
            await client.PostAsync("/Presale/Checkout/AddToCart", new FormUrlEncodedContent(addForm), CancellationToken);
            await client.GetAsync("/Presale/Checkout/PlaceOrder", CancellationToken);

            var placeForm = new Dictionary<string, string>
            {
                ["CurrencyId"] = "1",
                ["FirstName"] = "Jan",
                ["LastName"] = "Kowalski",
                ["Email"] = UniqueEmail("login-recovery"),
                ["PhoneNumber"] = "500100200",
                ["IsCompany"] = "false",
                ["Street"] = "Testowa",
                ["BuildingNumber"] = "1",
                ["ZipCode"] = "00-001",
                ["City"] = "Warszawa",
                ["Country"] = "Polska",
                ["__RequestVerificationToken"] = antiForgeryToken
            };
            var placeResponse = await client.PostAsync(
                "/Presale/Checkout/PlaceOrder",
                new FormUrlEncodedContent(placeForm),
                CancellationToken);
            var token = ParseQuery(placeResponse.RequestMessage!.RequestUri!)["token"];

            token.ShouldNotBeNullOrWhiteSpace();
            var login = await client.GetAsync($"/Identity/Account/Login?guestOrder={token}", CancellationToken);
            var html = await login.Content.ReadAsStringAsync();

            login.StatusCode.ShouldBe(HttpStatusCode.OK);
            html.ShouldContain("RequestGuestRecovery");
            html.ShouldContain(token);
        }

        [Fact]
        public async Task Login_WithoutGuestOrderQuery_DoesNotShowRecoverySection()
        {
            var client = CreateClient();

            var response = await client.GetAsync("/Identity/Account/Login", CancellationToken);
            var html = await response.Content.ReadAsStringAsync();

            response.StatusCode.ShouldBe(HttpStatusCode.OK);
            html.ShouldNotContain("RequestGuestRecovery");
        }

        [Fact]
        public async Task Login_WithGarbageGuestOrderToken_DoesNotShowRecoverySection()
        {
            var client = CreateClient();

            var response = await client.GetAsync("/Identity/Account/Login?guestOrder=not-a-real-token", CancellationToken);
            var html = await response.Content.ReadAsStringAsync();

            response.StatusCode.ShouldBe(HttpStatusCode.OK);
            html.ShouldNotContain("RequestGuestRecovery");
        }

        [Fact]
        public async Task PaymentsCreate_AnonymousWithOrderAccessCookie_AllowsOwnOrder_RejectsOtherOrder()
        {
            var productId = await _factory.CreateAvailableProductAsync();

            var clientA = CreateClient();
            await MintGuestCookieAsync(clientA);
            var tokenA = await FetchAntiForgeryTokenAsync(clientA, AnonymousTokenSourceUrl);
            var (orderIdA, _) = await PlaceGuestOrderAsync(clientA, tokenA, productId, UniqueEmail("payments-own"));

            // This test factory doesn't wire up automatic Payment creation on order placement, so a
            // 404 (no pending payment yet) is an expected, valid outcome here — what matters is that
            // the request reached PaymentsController.Create's own-order branch at all instead of being
            // redirected to login, which is what an access-cookie rejection would produce.
            var ownResponse = await clientA.GetAsync($"/Sales/Payments/Create/{orderIdA}", CancellationToken);
            ownResponse.RequestMessage!.RequestUri!.AbsolutePath.ShouldNotContain(
                "/Identity/Account/Login", Case.Insensitive,
                "the order-access cookie should authorize the anonymous caller for their own order's payment page, not redirect to login");

            var clientB = CreateClient();
            await MintGuestCookieAsync(clientB);
            var tokenB = await FetchAntiForgeryTokenAsync(clientB, AnonymousTokenSourceUrl);
            var (orderIdB, _) = await PlaceGuestOrderAsync(clientB, tokenB, productId, UniqueEmail("payments-other"));

            var otherResponse = await clientA.GetAsync($"/Sales/Payments/Create/{orderIdB}", CancellationToken);
            otherResponse.RequestMessage!.RequestUri!.AbsolutePath.ShouldContain(
                "/Identity/Account/Login", Case.Insensitive,
                "clientA's order-access cookie is scoped to order A and must not unlock order B's payment page");
        }

        [Fact]
        public async Task SalesOrdersDetails_AnonymousWithOrderAccessCookie_AllowsOwnOrder_RejectsOtherOrder()
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
                "/Identity/Account/Login", Case.Insensitive,
                "clientA's order-access cookie is scoped to order A and must not unlock order B's details page");
        }

        [Fact]
        public async Task RecoveryCode_ForOrderA_DoesNotGrantAccessToOrderB()
        {
            var productId = await _factory.CreateAvailableProductAsync();

            var clientA = CreateClient();
            await MintGuestCookieAsync(clientA);
            var tokenA = await FetchAntiForgeryTokenAsync(clientA, AnonymousTokenSourceUrl);
            var (orderIdA, _) = await PlaceGuestOrderAsync(clientA, tokenA, productId, UniqueEmail("recovery-a"));

            var clientB = CreateClient();
            await MintGuestCookieAsync(clientB);
            var tokenB = await FetchAntiForgeryTokenAsync(clientB, AnonymousTokenSourceUrl);
            var (orderIdB, _) = await PlaceGuestOrderAsync(clientB, tokenB, productId, UniqueEmail("recovery-b"));

            string code;
            using (var scope = _factory.Services.CreateScope())
            {
                var verificationCodeClient = scope.ServiceProvider.GetRequiredService<IVerificationCodeClient>();
                code = await verificationCodeClient.RequestOrderAccessRecoveryAsync(orderIdA, CancellationToken);
            }

            var recoveryClient = CreateClient(allowAutoRedirect: false);
            var redeemResponse = await recoveryClient.GetAsync($"/Presale/Checkout/RedeemRecovery?code={code}", CancellationToken);
            redeemResponse.StatusCode.ShouldBe(HttpStatusCode.Redirect, "redeeming a valid recovery code should mint the access cookie and redirect to Summary");

            var ownOrderSummary = await recoveryClient.GetAsync($"/Presale/Checkout/Summary/{orderIdA}", CancellationToken);
            ownOrderSummary.StatusCode.ShouldBe(HttpStatusCode.OK, "the redeemed code's order-access cookie should grant access to its own order");

            var otherOrderSummary = await recoveryClient.GetAsync($"/Presale/Checkout/Summary/{orderIdB}", CancellationToken);
            otherOrderSummary.StatusCode.ShouldBe(HttpStatusCode.Redirect,
                "a recovery code minted for order A must not grant access to order B, even though both orders exist in this test run");
            otherOrderSummary.Headers.Location!.OriginalString.ShouldContain("/Identity/Account/Login");
        }
    }
}
