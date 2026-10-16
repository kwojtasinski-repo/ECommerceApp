using ECommerceApp.Web.IntegrationTests.Presale.Checkout;
using Shouldly;
using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using Xunit;

namespace ECommerceApp.Web.IntegrationTests.AccountProfile
{
    /// <summary>
    /// Ownership (IDOR) guard on <c>ProfileController</c> POST Edit / EditContactInfo, exercised through
    /// the full MVC pipeline (cookie auth, antiforgery, routing, model binding).
    ///
    /// Observed pipeline behavior: the controller returns <c>Forbid()</c>, but the app uses the stock
    /// Identity cookie scheme, which converts a forbid into a 302 to <c>/Identity/Account/AccessDenied</c>;
    /// a literal 403 is never visible on the wire (same precedent as
    /// <c>GuestPromotionIntegrationTests</c>). Tests therefore assert the 302 + AccessDenied location,
    /// and, the part that actually matters, that the stored profile did not change.
    /// </summary>
    public class ProfileOwnershipIntegrationTests : GuestCheckoutTestBase, IClassFixture<GuestCheckoutTestFactory>
    {
        // Seeded by Utilities.InitializeIamUsers: a non-admin, non-maintenance user.
        private const string OwnerEmail = "test2@test2";
        private const string OwnerUserId = "e4fc1feb-7d08-4207-bd52-3f3464a01564";
        private const string Password = "Test@test12";

        public ProfileOwnershipIntegrationTests(GuestCheckoutTestFactory factory, ITestOutputHelper output)
            : base(factory, output) { }

        [Fact]
        public async Task Edit_NonOwner_RedirectsToAccessDeniedAndDoesNotMutate()
        {
            var profileId = await SeedOwnerProfileAsync();
            var intruderEmail = UniqueEmail("edit-intruder");
            await _factory.CreateRegisteredUserAsync(intruderEmail, Password);
            using var client = await LoginAsync(intruderEmail, Password);
            var token = await FetchAntiForgeryTokenAsync(client, AnonymousTokenSourceUrl);

            var response = await client.PostAsync(
                $"/AccountProfile/Profile/Edit/{profileId}",
                new FormUrlEncodedContent(EditForm("Hacked", "Hacked", token)),
                CancellationToken);

            ShouldBeAccessDeniedRedirect(response);
            var profile = await _factory.FindProfileByIdAsync(profileId);
            profile.FirstName.ShouldBe("Jan");
            profile.LastName.ShouldBe("Kowalski");
        }

        [Fact]
        public async Task EditContactInfo_NonOwner_RedirectsToAccessDeniedAndDoesNotMutate()
        {
            var profileId = await SeedOwnerProfileAsync();
            var originalEmail = (await _factory.FindProfileByIdAsync(profileId)).Email.Value;
            var intruderEmail = UniqueEmail("contact-intruder");
            await _factory.CreateRegisteredUserAsync(intruderEmail, Password);
            using var client = await LoginAsync(intruderEmail, Password);
            var token = await FetchAntiForgeryTokenAsync(client, AnonymousTokenSourceUrl);

            var response = await client.PostAsync(
                $"/AccountProfile/Profile/EditContactInfo/{profileId}",
                new FormUrlEncodedContent(ContactForm("evil@attacker.test", "999999999", token)),
                CancellationToken);

            ShouldBeAccessDeniedRedirect(response);
            var profile = await _factory.FindProfileByIdAsync(profileId);
            profile.Email.Value.ShouldBe(originalEmail);
            profile.PhoneNumber.Value.ShouldBe("500600700");
        }

        [Fact]
        public async Task Edit_Owner_UpdatesProfileAndRedirectsToDetails()
        {
            var profileId = await SeedOwnerProfileAsync();
            using var client = await LoginAsync(OwnerEmail, Password);
            var token = await FetchAntiForgeryTokenAsync(client, AnonymousTokenSourceUrl);

            var response = await client.PostAsync(
                $"/AccountProfile/Profile/Edit/{profileId}",
                new FormUrlEncodedContent(EditForm("Adam", "Nowak", token)),
                CancellationToken);

            response.StatusCode.ShouldBe(HttpStatusCode.Redirect);
            response.Headers.Location!.ToString().ShouldContain($"/AccountProfile/Profile/Details/{profileId}", Case.Insensitive);
            var profile = await _factory.FindProfileByIdAsync(profileId);
            profile.FirstName.ShouldBe("Adam");
            profile.LastName.ShouldBe("Nowak");
        }

        [Fact]
        public async Task EditContactInfo_Owner_UpdatesProfileAndRedirectsToDetails()
        {
            var profileId = await SeedOwnerProfileAsync();
            var newEmail = UniqueEmail("owner-new");
            using var client = await LoginAsync(OwnerEmail, Password);
            var token = await FetchAntiForgeryTokenAsync(client, AnonymousTokenSourceUrl);

            var response = await client.PostAsync(
                $"/AccountProfile/Profile/EditContactInfo/{profileId}",
                new FormUrlEncodedContent(ContactForm(newEmail, "700800900", token)),
                CancellationToken);

            response.StatusCode.ShouldBe(HttpStatusCode.Redirect);
            response.Headers.Location!.ToString().ShouldContain($"/AccountProfile/Profile/Details/{profileId}", Case.Insensitive);
            var profile = await _factory.FindProfileByIdAsync(profileId);
            profile.Email.Value.ShouldBe(newEmail);
            profile.PhoneNumber.Value.ShouldBe("700800900");
        }

        [Fact]
        public async Task Edit_MaintenanceRoleNonOwner_CanUpdate()
        {
            var profileId = await SeedOwnerProfileAsync();
            using var client = await LoginAsync(AdminEmail, AdminPassword);
            var token = await FetchAntiForgeryTokenAsync(client, AnonymousTokenSourceUrl);

            var response = await client.PostAsync(
                $"/AccountProfile/Profile/Edit/{profileId}",
                new FormUrlEncodedContent(EditForm("Staff", "Edited", token)),
                CancellationToken);

            response.StatusCode.ShouldBe(HttpStatusCode.Redirect);
            response.Headers.Location!.ToString().ShouldNotContain("AccessDenied", Case.Insensitive);
            (await _factory.FindProfileByIdAsync(profileId)).FirstName.ShouldBe("Staff");
        }

        private async Task<int> SeedOwnerProfileAsync()
        {
            return await _factory.SeedUnclaimedProfileAsync(UniqueEmail("owner"), OwnerUserId);
        }

        private async Task<HttpClient> LoginAsync(string email, string password)
        {
            var client = CreateClient(allowAutoRedirect: false);
            var token = await FetchAntiForgeryTokenAsync(client, AnonymousTokenSourceUrl);
            var response = await client.PostAsync(
                "/Identity/Account/Login",
                new FormUrlEncodedContent(new Dictionary<string, string>
                {
                    ["Input.Email"] = email,
                    ["Input.Password"] = password,
                    ["Input.RememberMe"] = "false",
                    ["__RequestVerificationToken"] = token
                }),
                CancellationToken);

            response.StatusCode.ShouldBe(HttpStatusCode.Redirect, $"login as {email} must succeed (redirect away from the login form)");
            response.Headers.Location!.ToString().ShouldNotContain("Login", Case.Insensitive);
            return client;
        }

        private static void ShouldBeAccessDeniedRedirect(HttpResponseMessage response)
        {
            response.StatusCode.ShouldBe(HttpStatusCode.Redirect);
            response.Headers.Location!.ToString().ShouldContain("AccessDenied", Case.Insensitive);
        }

        private static Dictionary<string, string> EditForm(string firstName, string lastName, string token) => new()
        {
            ["FirstName"] = firstName,
            ["LastName"] = lastName,
            ["IsCompany"] = "false",
            ["__RequestVerificationToken"] = token
        };

        private static Dictionary<string, string> ContactForm(string email, string phone, string token) => new()
        {
            ["email"] = email,
            ["phoneNumber"] = phone,
            ["__RequestVerificationToken"] = token
        };
    }
}
