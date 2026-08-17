using Shouldly;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using Xunit;

namespace ECommerceApp.Web.IntegrationTests.Presale.Checkout
{
    /// <summary>
    /// ADR-0030 Phase 3 — Guest account promotion, exercised through the real HTTP pipeline.
    ///
    /// <see cref="Promotion_DifferentGuestSession_CannotPromoteAnothersProfile_Returns403AndDoesNotMutate"/>
    /// is the single test called out as most critical across the whole 3-phase scope in
    /// <c>.github/plans/03-phase-guest-account-promotion-validation.md</c> Findings #1 — it protects the
    /// exact IDOR-style attack (guessing another guest's <c>profileId</c>) ADR-0030 §5 exists to prevent,
    /// and was previously verified only against a mocked repository
    /// (<c>GuestPromotionServiceTests.PromoteAsync_RequestingUserIdDoesNotMatchProfileOwner_ReturnsNotOwner</c>),
    /// never through <c>CheckoutController.CreateAccount</c> → <c>IShopperIdentityResolver</c> →
    /// <c>GuestPromotionService.PromoteAsync</c> → <c>Forbid()</c> end-to-end.
    /// </summary>
    public class GuestPromotionIntegrationTests
        : GuestCheckoutTestBase, IClassFixture<GuestCheckoutTestFactory>
    {
        public GuestPromotionIntegrationTests(GuestCheckoutTestFactory factory, ITestOutputHelper output)
            : base(factory, output) { }

        [Fact]
        public async Task Promotion_DifferentGuestSession_CannotPromoteAnothersProfile_Returns403AndDoesNotMutate()
        {
            // Guest A places a real order, creating a UserProfile that owns it.
            var clientA = CreateClient();
            var productA = await _factory.CreateAvailableProductAsync();
            var guestTokenA = await MintGuestCookieAsync(clientA);
            var tokenA = await FetchAntiForgeryTokenAsync(clientA, AnonymousTokenSourceUrl);
            var emailA = UniqueEmail("promo-victim");
            var (orderIdA, profileIdA) = await PlaceGuestOrderAsync(clientA, tokenA, productA, emailA);

            // Guest B is a *separate* cookie jar/HttpClient -> a distinct guest identity. Auto-redirect
            // is disabled so the raw, pre-redirect response from Forbid() can be observed directly —
            // ASP.NET Core Identity's cookie authentication scheme (used here with no customization,
            // confirmed via repo-wide grep for ConfigureApplicationCookie/OnRedirectToAccessDenied:
            // none exists) turns any MVC Forbid() into a 302 redirect to /Identity/Account/AccessDenied
            // before it reaches the wire — a literal 403 status code is never observable over HTTP for
            // this app's Cookie-scheme-authenticated actions, by design of the framework default, not a
            // gap in this app. The meaningful, security-relevant assertion is therefore that NotOwner
            // takes the distinctly different Forbid()/AccessDenied path rather than looking like success
            // or, critically, like ProfileNotFound's plain 404 (which would let profileId be enumerated).
            var clientB = CreateClient(allowAutoRedirect: false);
            var guestTokenB = await MintGuestCookieAsync(clientB);
            guestTokenB.ShouldNotBe(guestTokenA, "the two clients must resolve to genuinely different guest identities");
            var tokenB = await FetchAntiForgeryTokenAsync(clientB, AnonymousTokenSourceUrl);

            // Guest B attempts to promote guest A's profile by guessing/reusing its profileId.
            var attackForm = new Dictionary<string, string>
            {
                ["orderId"] = orderIdA.ToString(),
                ["profileId"] = profileIdA.ToString(),
                ["password"] = "Attacker@2026",
                ["__RequestVerificationToken"] = tokenB
            };
            var response = await clientB.PostAsync("/Presale/Checkout/CreateAccount", new FormUrlEncodedContent(attackForm), CancellationToken);

            // CheckoutController maps PromotionStatus.NotOwner => Forbid() — observed here as the raw
            // 302 to AccessDenied that Forbid() produces under Cookie auth (see comment above).
            response.StatusCode.ShouldBe(HttpStatusCode.Redirect);
            response.Headers.Location!.ToString().ShouldContain("AccessDenied");

            // Contrast with ProfileNotFound => NotFound(): a genuinely nonexistent profileId must NOT
            // produce the same signal as NotOwner (or as success) — otherwise a caller could enumerate
            // valid profileIds by distinguishing the two responses, exactly what ADR-0030 §5 forbids.
            var nonExistentProfileForm = new Dictionary<string, string>(attackForm) { ["profileId"] = "2147483647" };
            var notFoundResponse = await clientB.PostAsync("/Presale/Checkout/CreateAccount", new FormUrlEncodedContent(nonExistentProfileForm), CancellationToken);
            notFoundResponse.StatusCode.ShouldBe(HttpStatusCode.NotFound);

            // No state was mutated: profile A is unchanged (still owned by guest A's own token)...
            var profileAAfter = await _factory.FindProfileByUserIdAsync(guestTokenA);
            profileAAfter.ShouldNotBeNull("the ownership check must run before any mutation — profile A must still be found by guest A's own token");
            profileAAfter.Id.Value.ShouldBe(profileIdA);

            // ...no ApplicationUser was created against profile A's email as a side effect of the attack...
            (await _factory.ApplicationUserExistsAsync(emailA)).ShouldBeFalse();

            // ...and guest B was not granted any profile of its own by this attempt either.
            (await _factory.FindProfileByUserIdAsync(guestTokenB)).ShouldBeNull();
        }

        [Fact]
        public async Task Promotion_OwnProfile_HappyPath_Succeeds()
        {
            var client = CreateClient();
            var productId = await _factory.CreateAvailableProductAsync();
            var guestToken = await MintGuestCookieAsync(client);
            var token = await FetchAntiForgeryTokenAsync(client, AnonymousTokenSourceUrl);
            var email = UniqueEmail("promo-owner");
            var (orderId, profileId) = await PlaceGuestOrderAsync(client, token, productId, email);

            // PlaceGuestOrderAsync's PlaceOrder POST mints a GuestAccess sign-in, so the client's
            // identity changes from anonymous to authenticated mid-test. The antiforgery token fetched
            // above embeds the (then-anonymous) identity and is no longer valid for a request made after
            // that sign-in — fetch a fresh one from the Summary page (the same page CreateAccount's own
            // form lives on in the real UI). The CreateAccount form only renders when
            // ViewBag.GuestProfileId is set, which Summary only does from a `guest=True&profileId=`
            // query string — the same one PlaceOrder's own redirect always carries.
            var tokenAfterSignIn = await FetchAntiForgeryTokenAsync(
                client, $"/Presale/Checkout/Summary/{orderId}?profileId={profileId}&guest=True");
            var form = new Dictionary<string, string>
            {
                ["orderId"] = orderId.ToString(),
                ["profileId"] = profileId.ToString(),
                ["password"] = "GuestPass@2026",
                ["__RequestVerificationToken"] = tokenAfterSignIn
            };
            var response = await client.PostAsync("/Presale/Checkout/CreateAccount", new FormUrlEncodedContent(form), CancellationToken);

            response.StatusCode.ShouldBe(HttpStatusCode.OK);
            response.RequestMessage!.RequestUri!.AbsolutePath.ShouldContain("Summary", Case.Insensitive);

            (await _factory.ApplicationUserExistsAsync(email)).ShouldBeTrue("promotion must create a real ApplicationUser for the guest's email");

            // UserProfile.Id and Order.CustomerId are untouched by promotion (ReassignOwner only
            // updates UserId) — ADR-0030 §5: "No order is rewritten."
            (await _factory.GetOrderCustomerIdAsync(orderId)).ShouldBe(profileId);

            var profileAfter = await _factory.FindProfileByIdAsync(profileId);
            profileAfter.ShouldNotBeNull();
            profileAfter.Id.Value.ShouldBe(profileId);
            profileAfter.UserId.ShouldNotBe(guestToken, "ReassignOwner must have swapped the guest token for the new ApplicationUser's id");

            // The old guest token no longer resolves to this profile.
            (await _factory.FindProfileByUserIdAsync(guestToken)).ShouldBeNull();
        }

        [Fact]
        public async Task Promotion_AlreadyRegisteredProfile_ReturnsConflict()
        {
            // A profile whose UserId already equals a real, existing ApplicationUser id (as if it had
            // already gone through promotion, or been created for an authenticated user directly).
            var authClient = await CreateAuthenticatedClientAsync();
            var profileId = await _factory.EnsureProfileForRegisteredUserAsync(AdminUserId, UniqueEmail("already-registered"));
            var token = await FetchAntiForgeryTokenAsync(authClient, "/Currencies/Currency/Create");

            var form = new Dictionary<string, string>
            {
                ["orderId"] = "1",
                ["profileId"] = profileId.ToString(),
                ["password"] = "Whatever@123",
                ["__RequestVerificationToken"] = token
            };
            var response = await authClient.PostAsync("/Presale/Checkout/CreateAccount", new FormUrlEncodedContent(form), CancellationToken);

            // CheckoutController maps PromotionStatus.AlreadyRegistered => Conflict() (409).
            response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        }
    }
}
