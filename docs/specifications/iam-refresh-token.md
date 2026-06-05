# Flow: IAM Refresh Token

> Domain: Customers (Identity and Access)
> Status: Draft
> Last verified: 2026-06-05
> Governing ADR: [docs/adr/0019/README.md](docs/adr/0019/README.md)
> Related roadmap context: [docs/roadmap/iam-refresh-token.md](docs/roadmap/iam-refresh-token.md)

---

## Purpose

Describe only the refresh-token behavior that is currently implemented in the application.

---

## Scope (current code only)

### Included

- Login creates JWT + refresh token (`SignInAsync`).
- Refresh validates token, checks revoked/expired, revokes old token, issues new pair (`RefreshAsync`).
- Explicit token revocation (`RevokeAsync`).
- Reuse of already revoked token triggers user-wide revocation (`RevokeAllForUserAsync`) and error.

### Excluded

- Any event-driven IAM lifecycle not present in code.
- UI/navigation details.
- Persistence internals beyond observable business behavior.

---

## API endpoints in use

- `POST api/auth/login` -> `SignInAsync`
- `POST api/auth/refresh` -> `RefreshAsync`
- `POST api/auth/revoke` -> `RevokeAsync`

Source: `ECommerceApp.API/Controllers/IAM/AuthController.cs`.

---

## Implemented operations

### Sign-in path

1. Credentials validated via sign-in manager.
2. JWT issued.
3. Refresh token generated and persisted.
4. Response returns JWT + refresh token.

Source: `ECommerceApp.Application/Identity/IAM/Services/AuthenticationService.cs` (`SignInAsync`).

### Refresh path

1. Token loaded via repository (`GetByTokenAsync`).
2. If missing -> `BusinessException("Invalid refresh token")`.
3. If revoked -> `RevokeAllForUserAsync(...)` + `BusinessException("Refresh token has been revoked — possible theft detected")`.
4. If expired -> `BusinessException("Refresh token has expired")`.
5. Current token revoked via `stored.Revoke()`.
6. New JWT + new refresh token created and persisted.
7. Response returns new token pair.

Sources:

- `ECommerceApp.Application/Identity/IAM/Services/AuthenticationService.cs` (`RefreshAsync`)
- `ECommerceApp.Domain/Identity/IAM/RefreshToken.cs` (`Revoke`)

### Revoke path

1. Token loaded via repository (`GetByTokenAsync`).
2. If missing -> `BusinessException("Invalid refresh token")`.
3. Token revoked via `stored.Revoke()`.
4. Repository update persisted.

Source: `ECommerceApp.Application/Identity/IAM/Services/AuthenticationService.cs` (`RevokeAsync`).

---

## Effective token outcomes

- Active: token exists, not revoked, not expired.
- Revoked: `IsRevoked = true`.
- Expired: `ExpiresAt <= UtcNow`.
- Invalid: token not found.

These outcomes are implemented via checks in `RefreshAsync` and `RevokeAsync`, not via separate IAM message contracts.

---

## Error outcomes (1:1 with current logic)

- `Invalid refresh token`
- `Refresh token has expired`
- `Refresh token has been revoked — possible theft detected`
- `User not found`

Source: `ECommerceApp.Application/Identity/IAM/Services/AuthenticationService.cs`.

---

## Business rules implemented now

- Refresh requires an existing token.
- Expired token cannot be refreshed.
- Revoked token cannot be refreshed.
- Successful refresh revokes old token and returns a new pair.
- Revoked-token refresh attempt triggers `RevokeAllForUserAsync` before failing.
- Explicit revoke only marks token revoked (no new token issuance).

---

## Notes on current implementation boundaries

- This flow is implemented as service operations and endpoint calls.
- No dedicated IAM integration-event taxonomy is implemented for the refresh lifecycle.
