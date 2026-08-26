# ADR-0030: Verified Implementation Details

This companion records the shipped implementation behind [ADR-0030](../0030-guest-checkout-anonymous-order-placement.md).
It is an implementation reference, not a second decision record. Names and paths below were
verified against the current C# source.

## Guest identity and checkout

- `ECommerceApp.Web/Areas/Presale/Services/IShopperIdentityResolver.cs` and
  `ShopperIdentityResolver.cs` resolve the session identity.
- `ECommerceApp.Web/Areas/Presale/GuestSession.cs` defines the guest cookie.
- `ECommerceApp.Web/Areas/Presale/Controllers/CheckoutController.cs` uses the resolver for the
  anonymous cart and checkout actions.
- `ECommerceApp.Domain/Presale/Checkout/PresaleUserId.cs` is the checkout identity value object.
- `ECommerceApp.Application/Presale/Checkout/Contracts/IAccountProfileClient.cs` exposes
  `EnsureGuestCustomerAsync`; the AccountProfile service uses
  `IUserProfileService.GetOrCreateForGuestAsync`.
- `ECommerceApp.Domain/AccountProfile/UserProfile.cs` provides `Create` and `ReassignOwner`.
- `ECommerceApp.Application/AccountProfile/Services/GuestPromotionService.cs` performs the
  ownership check and promotion.

The API checkout controllers are not part of this implementation.

## Verification and recovery

- `ECommerceApp.Domain/Supporting/Verification/VerificationCode.cs` and
  `VerificationPurpose.cs` define the shared primitive and its purposes.
- `ECommerceApp.Application/Supporting/Verification/Services/VerificationCodeService.cs` and
  `ECommerceApp.Infrastructure/Supporting/Verification/` provide the service, repository,
  configuration, context, and migration.
- `ECommerceApp.Web/Areas/Backoffice/Controllers/GuestVerificationController.cs` is the
  administrator-only interim view.
- `ECommerceApp.Domain/Presale/Checkout/OrderAccessToken.cs` and
  `ECommerceApp.Web/Areas/Presale/OrderAccessTokenGenerator.cs` provide the persisted backing
  token.
- `ECommerceApp.Web/Areas/Presale/Authorization/GuestAccessScope.cs`,
  `OrderAccessRequirement.cs`, and `OrderAccessAuthorizationHandler.cs` enforce the one-order
  resource scope. `IOrderAccessAuthorizer` is the MVC adapter.
- `CheckoutController` exposes `Order`, `RequestOrderAccess`, and `ConfirmOrderAccess`; the
  latter two carry the `OrderAccessByOrderId` rate-limit policy.
- `ECommerceApp.Web/Startup.cs` registers the `GuestAccess` scheme, the `OrderAccess` policy, and
  the rate-limit configuration.

There is no shipped `OrderAccessCookie` type and no `IOrderAccessClient`; those names belong to
superseded design text and are not implementation references.

## Verified regression coverage

- `ECommerceApp.Web.IntegrationTests/Presale/Checkout/GuestCheckoutAllowlistTests.cs` guards the
  closed anonymous action list.
- `ECommerceApp.Web.IntegrationTests/Presale/Checkout/SessionIsolationTests.cs` checks that
  guest and authenticated decoy sessions cannot be crossed.
- `ECommerceApp.UnitTests/Web/OrderAccessAuthorizationHandlerTests.cs` checks ownership,
  maintenance-role bypass, and the single-order guest scope.
- `ECommerceApp.Web.IntegrationTests/Presale/Checkout/OrderAccessRecoveryIntegrationTests.cs`
  covers recovery and payment access.
- `ECommerceApp.Web.IntegrationTests/Presale/Checkout/GuestCheckoutRateLimitingIntegrationTests.cs`
  covers the `429` and `Retry-After` behavior.
- `ECommerceApp.Web.E2E/GuestCheckoutLifecycleTests.cs` covers the browser lifecycle, including
  guest access isolation.
