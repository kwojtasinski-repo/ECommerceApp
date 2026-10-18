using System;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using ECommerceApp.Domain.Identity.IAM;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Shouldly;
using Xunit;

namespace ECommerceApp.E2E.Backend.Auth
{
    /// <summary>
    /// The API is token-only: a JWT Bearer token must authenticate a request, and the Identity cookie that
    /// <c>AddDefaultIdentity</c> registers (for the Web project) must never be the API's default scheme.
    /// <para>
    /// Every client here has <c>HandleCookies = false</c>. A default <c>WebApplicationFactory</c> client keeps cookies,
    /// which once hid the problem: login stored the Identity cookie, later requests carried it, and the Bearer header
    /// was never checked, so Bearer tests passed while a real token-only caller got 401.
    /// </para>
    /// </summary>
    [Collection(ApiAuthE2EFixture.CollectionName)]
    public class ApiAuthE2ETests
    {
        // Needs an authenticated caller; the seeded admin has the role it requires.
        private const string ProtectedPath = "/api/images";

        private readonly ApiAuthE2EFixture _fixture;

        public ApiAuthE2ETests(ApiAuthE2EFixture fixture)
        {
            _fixture = fixture;
        }

        [Fact]
        public async Task Login_ReturnsAccessToken_AndDoesNotIssueAnIdentityCookie()
        {
            using var client = _fixture.CreateCookielessClient();

            using var response = await LoginAsync(client);

            response.StatusCode.ShouldBe(HttpStatusCode.OK);
            (await ReadAccessTokenAsync(response)).ShouldNotBeNullOrWhiteSpace();
            response.Headers.Contains("Set-Cookie").ShouldBeFalse(
                "the API signs in with a JWT only; a cookie from the login endpoint would make the API depend on Identity cookie auth");
        }

        [Fact]
        public async Task ProtectedEndpoint_WithBearerToken_ReturnsOk()
        {
            using var client = _fixture.CreateCookielessClient();
            using var login = await LoginAsync(client);
            var token = await ReadAccessTokenAsync(login);

            using var request = new HttpRequestMessage(HttpMethod.Get, ProtectedPath);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            using var response = await client.SendAsync(request, TestContext.Current.CancellationToken);

            response.StatusCode.ShouldBe(HttpStatusCode.OK);
        }

        [Fact]
        public async Task ProtectedEndpoint_WithoutCredentials_Returns401WithBearerChallengeAndNoRedirect()
        {
            using var client = _fixture.CreateCookielessClient();

            using var response = await client.GetAsync(ProtectedPath, TestContext.Current.CancellationToken);

            response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
            response.Headers.WwwAuthenticate.Any(h => h.Scheme == "Bearer").ShouldBeTrue(
                "an API challenge must name the Bearer scheme");
            response.Headers.Location.ShouldBeNull("an API must not redirect to the Web login page");
        }

        [Fact]
        public async Task ProtectedEndpoint_WithOnlyAValidIdentityCookie_Returns401()
        {
            using var client = _fixture.CreateCookielessClient();
            var cookie = await CreateIdentityCookieAsync();

            using var request = new HttpRequestMessage(HttpMethod.Get, ProtectedPath);
            request.Headers.Add("Cookie", cookie);
            using var response = await client.SendAsync(request, TestContext.Current.CancellationToken);

            response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized,
                "a cookie is not a credential for the API; only a Bearer token is");
        }

        [Fact]
        public async Task ProtectedEndpoint_WithGarbageBearerToken_Returns401()
        {
            using var client = _fixture.CreateCookielessClient();

            using var request = new HttpRequestMessage(HttpMethod.Get, ProtectedPath);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", "not-a-jwt");
            using var response = await client.SendAsync(request, TestContext.Current.CancellationToken);

            response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        }

        private static async Task<HttpResponseMessage> LoginAsync(HttpClient client)
        {
            var body = JsonSerializer.Serialize(new
            {
                email = ApiAuthE2EFixture.SeededAdminEmail,
                password = ApiAuthE2EFixture.SeededAdminPassword
            });
            using var content = new StringContent(body, Encoding.UTF8, "application/json");
            return await client.PostAsync("/api/auth/login", content, TestContext.Current.CancellationToken);
        }

        private static async Task<string> ReadAccessTokenAsync(HttpResponseMessage response)
        {
            var json = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
            using var document = JsonDocument.Parse(json);
            foreach (var property in document.RootElement.EnumerateObject())
            {
                if (string.Equals(property.Name, "accessToken", StringComparison.OrdinalIgnoreCase))
                {
                    return property.Value.GetString();
                }
            }

            throw new InvalidOperationException($"No accessToken in login response: {json}");
        }

        // Builds the same cookie Identity would issue at sign-in, without a login call, so the test does not depend on
        // the login endpoint issuing (or not issuing) one.
        private async Task<string> CreateIdentityCookieAsync()
        {
            using var scope = _fixture.Factory.Services.CreateScope();
            var services = scope.ServiceProvider;

            var user = await services.GetRequiredService<UserManager<ApplicationUser>>()
                .FindByNameAsync(ApiAuthE2EFixture.SeededAdminEmail);
            user.ShouldNotBeNull();

            var principal = await services.GetRequiredService<IUserClaimsPrincipalFactory<ApplicationUser>>().CreateAsync(user);
            var options = services.GetRequiredService<IOptionsMonitor<CookieAuthenticationOptions>>()
                .Get(IdentityConstants.ApplicationScheme);
            var ticket = new AuthenticationTicket(principal, IdentityConstants.ApplicationScheme);

            return $"{options.Cookie.Name}={options.TicketDataFormat.Protect(ticket)}";
        }
    }
}
