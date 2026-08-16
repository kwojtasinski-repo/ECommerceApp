using ECommerceApp.Web.IntegrationTests.Presale.Checkout;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.AspNetCore.Mvc.Testing;
using Shouldly;
using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using System.Text.RegularExpressions;
using Xunit;

namespace ECommerceApp.Web.IntegrationTests.AccountProfile
{
    public class GuestAccountLinkingIntegrationTests : GuestCheckoutTestBase, IClassFixture<GuestCheckoutTestFactory>
    {
        public GuestAccountLinkingIntegrationTests(GuestCheckoutTestFactory factory, ITestOutputHelper output)
            : base(factory, output) { }

        [Fact]
        public async Task Register_WithOrWithoutMatchingGuestProfiles_ReturnsIdenticalResponse()
        {
            var matchingEmail = "linking-match@guest.test";
            await _factory.SeedUnclaimedProfileAsync(matchingEmail, "gst_matching");

            using var noMatchFactory = new GuestCheckoutTestFactory();
            var matchingResponse = await RegisterAsync(_factory, matchingEmail);
            var noMatchResponse = await RegisterAsync(noMatchFactory, matchingEmail);

            matchingResponse.StatusCode.ShouldBe(noMatchResponse.StatusCode);
            NormalizeAntiForgery(await matchingResponse.Content.ReadAsStringAsync(CancellationToken))
                .ShouldBe(NormalizeAntiForgery(await noMatchResponse.Content.ReadAsStringAsync(CancellationToken)));
        }

        [Fact]
        public async Task LinkGuestOrders_Anonymous_RedirectsToLoginWithCodePreserved()
        {
            var code = await CreateCodeAsync("linking-anonymous@guest.test");
            using var client = CreateClient(allowAutoRedirect: false);

            var response = await client.GetAsync($"/Identity/Account/LinkGuestOrders?code={code}", CancellationToken);

            response.StatusCode.ShouldBe(HttpStatusCode.Redirect);
            response.Headers.Location!.ToString().ShouldContain("Login");
            response.Headers.Location!.ToString().ShouldContain(Uri.EscapeDataString($"LinkGuestOrders?code={code}"));
        }

        [Fact]
        public async Task LinkGuestOrders_ExpiredCode_FailsGenerically()
        {
            var code = await CreateCodeAsync("linking-expired@guest.test", validFor: TimeSpan.FromSeconds(-1));
            using var client = await CreateAuthenticatedClientAsync();

            var response = await client.GetAsync($"/Identity/Account/LinkGuestOrders?code={code}", CancellationToken);

            response.StatusCode.ShouldBe(HttpStatusCode.OK);
            var html = await response.Content.ReadAsStringAsync(CancellationToken);
            html.ShouldContain("Ten link jest nieaktualny.");
            html.ShouldNotContain("Połączono");
        }

        [Fact]
        public async Task LinkGuestOrders_ValidCode_ReassignsProfileWithoutChangingOrderCustomerId()
        {
            var email = "linking-order@guest.test";
            var productId = await _factory.CreateAvailableProductAsync();
            using var guestClient = CreateClient();
            await MintGuestCookieAsync(guestClient);
            var guestToken = await FetchAntiForgeryTokenAsync(guestClient, AnonymousTokenSourceUrl);
            var (orderId, profileId) = await PlaceGuestOrderAsync(guestClient, guestToken, productId, email);

            var code = await CreateCodeAsync(email);
            using var adminClient = await CreateAuthenticatedClientAsync();

            var response = await adminClient.GetAsync($"/Identity/Account/LinkGuestOrders?code={code}", CancellationToken);

            response.StatusCode.ShouldBe(HttpStatusCode.OK);
            (await response.Content.ReadAsStringAsync(CancellationToken)).ShouldContain("Połączono");

            var profile = await _factory.FindProfileByIdAsync(profileId);
            profile.UserId.ShouldBe(AdminUserId, "redemption must reassign to the authenticated caller, not to some id embedded in the code");

            var customerId = await _factory.GetOrderCustomerIdAsync(orderId);
            customerId.ShouldBe(profileId, "Order.CustomerId must be untouched by account-linking — only UserProfile.UserId changes");
        }

        [Fact]
        public async Task GuestVerificationIndex_NonAdminAuthenticated_IsDeniedAccess()
        {
            using var client = CreateClient();
            var token = await FetchAntiForgeryTokenAsync(client, "/Identity/Account/Login");
            await client.PostAsync(
                "/Identity/Account/Login",
                new FormUrlEncodedContent(new Dictionary<string, string>
                {
                    ["Input.Email"] = "test2@test2",
                    ["Input.Password"] = "Test@test12",
                    ["Input.RememberMe"] = "false",
                    ["__RequestVerificationToken"] = token
                }),
                CancellationToken);

            var response = await client.GetAsync("/Backoffice/GuestVerification", CancellationToken);

            response.RequestMessage!.RequestUri!.AbsolutePath.ShouldContain(
                "AccessDenied", Case.Insensitive, "a non-Administrator authenticated user must be denied, not shown the pending-codes list");
        }

        private static async Task<HttpResponseMessage> RegisterAsync(GuestCheckoutTestFactory factory, string email)
        {
            using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
            {
                AllowAutoRedirect = true,
                HandleCookies = true,
                BaseAddress = new System.Uri("https://localhost")
            });
            var registerPage = await client.GetAsync("/Identity/Account/Register");
            var registerHtml = await registerPage.Content.ReadAsStringAsync();
            const string tokenMarker = "name=\"__RequestVerificationToken\" type=\"hidden\" value=\"";
            var tokenStart = registerHtml.IndexOf(tokenMarker) + tokenMarker.Length;
            var tokenEnd = registerHtml.IndexOf('"', tokenStart);
            var token = registerHtml[tokenStart..tokenEnd];
            return await client.PostAsync(
                "/Identity/Account/Register",
                new FormUrlEncodedContent(new Dictionary<string, string>
                {
                    ["Input.Email"] = email,
                    ["Input.Password"] = "Test@test12",
                    ["Input.ConfirmPassword"] = "Test@test12",
                    ["__RequestVerificationToken"] = token
                }),
                CancellationToken);
        }

        private async Task<string> CreateCodeAsync(string email, System.TimeSpan? validFor = null)
        {
            using var scope = _factory.Services.CreateScope();
            var service = scope.ServiceProvider.GetRequiredService<ECommerceApp.Application.Supporting.Verification.Services.IVerificationCodeService>();
            return await service.GenerateAsync(
                ECommerceApp.Domain.Supporting.Verification.VerificationPurpose.GuestAccountLink,
                email,
                validFor ?? System.TimeSpan.FromDays(7),
                CancellationToken);
        }

        private static string NormalizeAntiForgery(string html)
        {
            const string marker = "name=\"__RequestVerificationToken\" type=\"hidden\" value=\"";
            var start = html.IndexOf(marker, System.StringComparison.Ordinal);
            if (start >= 0)
            {
                start += marker.Length;
                var end = html.IndexOf('"', start);
                html = end > start ? html.Remove(start, end - start) : html;
            }

            html = Regex.Replace(html, "(href|src|value)=\"[^\"]*\"", "$1=\"<dynamic>\"", RegexOptions.IgnoreCase);
            html = Regex.Replace(html, "[0-9a-f]{32,}", "<dynamic>", RegexOptions.IgnoreCase);
            html = Regex.Replace(html, "[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}", "<dynamic>", RegexOptions.IgnoreCase);
            return Regex.Replace(html, "\\s+", " ").Trim();
        }
    }
}