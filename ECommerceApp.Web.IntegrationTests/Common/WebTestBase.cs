using AngleSharp;
using ECommerceApp.Shared.TestInfrastructure;
using ECommerceApp.Web;
using Microsoft.AspNetCore.Mvc.Testing;
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading.Tasks;
using Xunit;
using Xunit.Abstractions;

namespace ECommerceApp.Web.IntegrationTests
{
    /// <summary>
    /// Base class for Web MVC integration tests that need to:
    /// <list type="bullet">
    ///   <item>Log in via the Identity cookie flow.</item>
    ///   <item>Extract anti-forgery tokens from rendered HTML before POSTing forms.</item>
    ///   <item>Parse the response HTML to assert that validation errors are rendered.</item>
    /// </list>
    /// </summary>
    public abstract class WebTestBase<TFactory> : IAsyncLifetime
        where TFactory : class, IHaveXunitSink
    {
        protected readonly TFactory _factory;
        protected readonly ITestOutputHelper _output;

        // Seeded Administrator user — matches CustomWebApplicationFactory seed
        protected const string AdminEmail = "test@test";
        protected const string AdminPassword = "Test@test12";

        protected WebTestBase(TFactory factory, ITestOutputHelper output)
        {
            _factory = factory;
            _output = output;
        }

        public virtual Task InitializeAsync()
        {
            _factory.Sink.SetOutput(_output);
            return Task.CompletedTask;
        }

        public virtual Task DisposeAsync()
        {
            _factory.Sink.SetOutput(null);
            return Task.CompletedTask;
        }

        // ─── HTTP helpers ────────────────────────────────────────────────

        /// <summary>Returns a new <see cref="HttpClient"/> that follows redirects and shares a cookie jar.</summary>
        protected HttpClient CreateClient()
            => (_factory as CustomWebApplicationFactory<Startup>)!
                .CreateClient(new WebApplicationFactoryClientOptions
                {
                    AllowAutoRedirect = true,
                    HandleCookies = true
                });

        /// <summary>Logs in via the Identity Razor Pages cookie flow and returns the authenticated client.</summary>
        protected async Task<HttpClient> CreateAuthenticatedClientAsync()
        {
            var client = CreateClient();

            // GET the login page to obtain the AntiForgery token
            var getResponse = await client.GetAsync("/Identity/Account/Login");
            getResponse.EnsureSuccessStatusCode();
            var loginHtml = await getResponse.Content.ReadAsStringAsync();
            var token = ExtractAntiForgeryToken(loginHtml);

            var loginForm = new Dictionary<string, string>
            {
                ["Input.Email"] = AdminEmail,
                ["Input.Password"] = AdminPassword,
                ["Input.RememberMe"] = "false",
                ["__RequestVerificationToken"] = token
            };

            var postResponse = await client.PostAsync(
                "/Identity/Account/Login",
                new FormUrlEncodedContent(loginForm));

            // The page redirects (302 → 200) on success; ensure we did not land back on the login page
            var finalUrl = postResponse.RequestMessage?.RequestUri?.AbsolutePath ?? "/";
            if (finalUrl.Contains("Login", StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException($"Cookie login failed — still on login page. Check credentials or seed. Final URL: {finalUrl}");

            return client;
        }

        // ─── CSRF helpers ────────────────────────────────────────────────

        /// <summary>
        /// GETs <paramref name="url"/>, finds the first hidden <c>__RequestVerificationToken</c>
        /// input and returns its value.
        /// </summary>
        protected async Task<string> FetchAntiForgeryTokenAsync(HttpClient client, string url)
        {
            var html = await client.GetStringAsync(url);
            return ExtractAntiForgeryToken(html);
        }

        private static string ExtractAntiForgeryToken(string html)
        {
            // Fast path: look for the hidden input without a full DOM parse
            const string marker = "name=\"__RequestVerificationToken\" type=\"hidden\" value=\"";
            var idx = html.IndexOf(marker, StringComparison.Ordinal);
            if (idx >= 0)
            {
                var start = idx + marker.Length;
                var end = html.IndexOf('"', start);
                if (end > start)
                    return html[start..end];
            }

            // Fallback: reverse attribute order emitted by older tag-helpers
            const string marker2 = "__RequestVerificationToken\" value=\"";
            idx = html.IndexOf(marker2, StringComparison.Ordinal);
            if (idx >= 0)
            {
                var start = idx + marker2.Length;
                var end = html.IndexOf('"', start);
                if (end > start)
                    return html[start..end];
            }

            throw new InvalidOperationException("Could not find __RequestVerificationToken in the page HTML.");
        }

        // ─── HTML parsing helpers ────────────────────────────────────────

        /// <summary>
        /// Parses <paramref name="html"/> with AngleSharp and returns all text content
        /// from elements matching <paramref name="cssSelector"/>.
        /// </summary>
        protected static async Task<IEnumerable<string>> ParseValidationErrorsAsync(string html, string cssSelector = ".text-danger")
        {
            var config = Configuration.Default;
            using var context = BrowsingContext.New(config);
            var document = await context.OpenAsync(req => req.Content(html));

            var errors = new List<string>();
            foreach (var el in document.QuerySelectorAll(cssSelector))
            {
                var text = el.TextContent?.Trim();
                if (!string.IsNullOrEmpty(text))
                    errors.Add(text);
            }
            return errors;
        }

        /// <summary>
        /// POSTs a form, then parses validation errors from the response HTML.
        /// </summary>
        protected async Task<(HttpResponseMessage Response, IEnumerable<string> Errors)>
            PostFormAndGetValidationErrorsAsync(HttpClient client, string url, Dictionary<string, string> form)
        {
            var response = await client.PostAsync(url, new FormUrlEncodedContent(form));
            var html = await response.Content.ReadAsStringAsync();
            var errors = await ParseValidationErrorsAsync(html);
            return (response, errors);
        }
    }
}

