using ECommerceApp.Web.IntegrationTests;
using Shouldly;
using System.Collections.Generic;
using System.Net;
using System.Threading.Tasks;
using Xunit;

namespace ECommerceApp.Web.IntegrationTests.Currencies
{
    /// <summary>
    /// Locks down the server-side validation pipeline for Web MVC area controllers.
    ///
    /// Architecture note: the Web project registers a global <c>ModelStateFilter</c>
    /// that intercepts all actions with an invalid ModelState and returns
    /// <c>400 application/json</c> BEFORE the controller action body runs.
    /// This means the "return View(model)" inside each controller is only reached
    /// when the filter is bypassed (e.g., via JavaScript validation on the frontend).
    ///
    /// Therefore, the server-side contract to pin here is:
    ///   Invalid form POST → 400 (ModelStateFilter)
    ///   Valid form POST   → redirect to Index (action success)
    ///
    /// The HTML validation-error rendering (spans with class="text-danger") is the
    /// responsibility of the JS layer (forms.js / jQuery unobtrusive), tested separately.
    ///
    /// This class covers the <c>Currencies</c> area, which is the only MVC area whose
    /// FormVm types have real FluentValidation validators.
    /// </summary>
    public class WebValidationBehaviorTests
        : WebTestBase<WebValidationTestFactory>, IClassFixture<WebValidationTestFactory>
    {
        public WebValidationBehaviorTests(WebValidationTestFactory factory, ITestOutputHelper output)
            : base(factory, output) { }

        // ─────────────────────────────────────────────────────────────────
        // Currencies / Create  (CreateCurrencyFormVmValidator + ModelStateFilter)
        // Route: /Currencies/Currency/Create
        // ─────────────────────────────────────────────────────────────────

        [Fact]
        public async Task CreateCurrency_EmptyCode_Returns400()
        {
            var client = await CreateAuthenticatedClientAsync();
            var token = await FetchAntiForgeryTokenAsync(client, "/Currencies/Currency/Create");

            var (response, _) = await PostFormAndGetValidationErrorsAsync(
                client,
                "/Currencies/Currency/Create",
                new Dictionary<string, string>
                {
                    ["Code"] = "",
                    ["Description"] = "Valid description",
                    ["__RequestVerificationToken"] = token
                });

            // ModelStateFilter intercepts before the action body → 400 JSON, not re-rendered view
            response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        }

        [Fact]
        public async Task CreateCurrency_InvalidCodeFormat_Returns400()
        {
            var client = await CreateAuthenticatedClientAsync();
            var token = await FetchAntiForgeryTokenAsync(client, "/Currencies/Currency/Create");

            // "xx" violates both Length(3) and Matches(@"^[A-Z]{3}$")
            var (response, _) = await PostFormAndGetValidationErrorsAsync(
                client,
                "/Currencies/Currency/Create",
                new Dictionary<string, string>
                {
                    ["Code"] = "xx",
                    ["Description"] = "Valid description",
                    ["__RequestVerificationToken"] = token
                });

            response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        }

        [Fact]
        public async Task CreateCurrency_EmptyDescription_Returns400()
        {
            var client = await CreateAuthenticatedClientAsync();
            var token = await FetchAntiForgeryTokenAsync(client, "/Currencies/Currency/Create");

            var (response, _) = await PostFormAndGetValidationErrorsAsync(
                client,
                "/Currencies/Currency/Create",
                new Dictionary<string, string>
                {
                    ["Code"] = "EUR",
                    ["Description"] = "",
                    ["__RequestVerificationToken"] = token
                });

            response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        }

        [Fact]
        public async Task CreateCurrency_DescriptionTooShort_Returns400()
        {
            var client = await CreateAuthenticatedClientAsync();
            var token = await FetchAntiForgeryTokenAsync(client, "/Currencies/Currency/Create");

            // MinimumLength(3) — "AB" is 2 chars
            var (response, _) = await PostFormAndGetValidationErrorsAsync(
                client,
                "/Currencies/Currency/Create",
                new Dictionary<string, string>
                {
                    ["Code"] = "EUR",
                    ["Description"] = "AB",
                    ["__RequestVerificationToken"] = token
                });

            response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        }

        [Fact]
        public async Task CreateCurrency_ValidData_RedirectsToIndex()
        {
            var client = await CreateAuthenticatedClientAsync();
            var token = await FetchAntiForgeryTokenAsync(client, "/Currencies/Currency/Create");

            // AllowAutoRedirect=true → we land on Index after a successful POST
            var (response, _) = await PostFormAndGetValidationErrorsAsync(
                client,
                "/Currencies/Currency/Create",
                new Dictionary<string, string>
                {
                    ["Code"] = "JPY",
                    ["Description"] = "Japanese Yen",
                    ["__RequestVerificationToken"] = token
                });

            // After redirect the final page is Index — its URL does not contain "Create"
            var finalPath = response.RequestMessage?.RequestUri?.AbsolutePath ?? "";
            finalPath.ShouldNotContain("Create", Case.Insensitive);
        }

        // ─────────────────────────────────────────────────────────────────
        // Currencies / Edit  (UpdateCurrencyFormVmValidator + ModelStateFilter)
        // Route: /Currencies/Currency/Edit
        // The seeded database has currencies with Id 2,3,4,5.
        // ─────────────────────────────────────────────────────────────────

        [Fact]
        public async Task EditCurrency_EmptyCode_Returns400()
        {
            var client = await CreateAuthenticatedClientAsync();
            var token = await FetchAntiForgeryTokenAsync(client, "/Currencies/Currency/Edit/2");

            var (response, _) = await PostFormAndGetValidationErrorsAsync(
                client,
                "/Currencies/Currency/Edit",
                new Dictionary<string, string>
                {
                    ["Id"] = "2",
                    ["Code"] = "",
                    ["Description"] = "Valid description",
                    ["__RequestVerificationToken"] = token
                });

            response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        }

        [Fact]
        public async Task EditCurrency_EmptyDescription_Returns400()
        {
            var client = await CreateAuthenticatedClientAsync();
            var token = await FetchAntiForgeryTokenAsync(client, "/Currencies/Currency/Edit/2");

            var (response, _) = await PostFormAndGetValidationErrorsAsync(
                client,
                "/Currencies/Currency/Edit",
                new Dictionary<string, string>
                {
                    ["Id"] = "2",
                    ["Code"] = "EUR",
                    ["Description"] = "",
                    ["__RequestVerificationToken"] = token
                });

            response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        }
    }
}
