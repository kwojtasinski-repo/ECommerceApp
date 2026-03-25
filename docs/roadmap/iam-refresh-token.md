# Roadmap: Identity/IAM — Refresh Token Feature

> ADR: [ADR-0019](../adr/0019-identity-iam-bc-design.md) — Identity/IAM BC Design (amendment pending — see note below)
> Status: 🔵 Planned — design decisions settled, implementation not started
> **Fits inside**: IAM BC — implement before or alongside `iam-atomic-switch.md` Step 2

---

## Design decisions (settled)

| Decision | Choice | Rationale |
|---|---|---|
| Token storage | **DB via EF Core** (`IamDbContext`) | Consistent with existing stack; no new infrastructure |
| Token–access-token binding | **`Jti` claim** (already in every token via `JwtManager`) | No new `SessionId` concept needed for this iteration |
| Session tracking | **Deferred** | One refresh token per sign-in is sufficient now; `SessionId` column can be added in a later migration without breaking changes |
| Rotation strategy | **Rotation on use** — old token revoked, new pair issued | Standard security practice; enables theft detection |
| Theft detection | **Revoke entire chain** if a revoked token is reused | Detected via `IsRevoked` + `JwtId` lookup |

---

## What already exists (scaffold)

| File | What's there |
|---|---|
| `Application/Identity/IAM/DTOs/SignInResponseDto.cs` | `record SignInResponseDto(string AccessToken, string RefreshToken)` — `RefreshToken` always `""` today |
| `Infrastructure/Identity/IAM/Auth/JwtManager.cs` | Issues `Jti` claim (`Guid.NewGuid()`) on every token — the binding key is already there |
| `Application/Identity/IAM/Services/AuthenticationService.cs` | `SignInAsync` issues token, returns `SignInResponseDto("token", "")` — stub ready to be filled |

---

## Target entity

```csharp
// Domain/Identity/IAM/RefreshToken.cs
public class RefreshToken
{
    public int Id { get; private set; }
    public string UserId { get; private set; }
    public string Token { get; private set; }       // opaque, cryptographically random
    public string JwtId { get; private set; }       // Jti of the paired access token
    public bool IsRevoked { get; private set; }
    public DateTime ExpiresAt { get; private set; }
    public DateTime CreatedAt { get; private set; }
}
```

> `SessionId` column intentionally omitted. Add in a future migration as a nullable `Guid?`
> when "active sessions" backoffice view is needed.

---

## Implementation steps

### Step 1 — Domain entity + repository interface

| File | Action |
|---|---|
| `Domain/Identity/IAM/RefreshToken.cs` | New entity — `Create(userId, token, jwtId, expiresAt)`, `Revoke()` method |
| `Domain/Identity/IAM/IRefreshTokenRepository.cs` | `AddAsync`, `GetByTokenAsync`, `RevokeAllForUserAsync` |

---

### Step 2 — EF Core configuration + migration

| File | Action |
|---|---|
| `Infrastructure/Identity/IAM/Configurations/RefreshTokenConfiguration.cs` | Map to `iam.RefreshTokens`; index on `Token` (unique); index on `UserId` |
| `Infrastructure/Identity/IAM/IamDbContext.cs` | Add `DbSet<RefreshToken>` |
| `Infrastructure/Identity/IAM/Repositories/RefreshTokenRepository.cs` | Implement `IRefreshTokenRepository` |
| Migration `AddRefreshTokensTable` | **Requires approval per migration policy before going to production** |

---

### Step 3 — Update `IJwtManager` + `JwtManager`

| File | Change |
|---|---|
| `Application/Interfaces/IJwtManager.cs` | Return `(string Token, string Jti)` tuple instead of bare `string` |
| `Infrastructure/Identity/IAM/Auth/JwtManager.cs` | Return the `Jti` alongside the token so `AuthenticationService` can store it |

> **Non-breaking**: callers that only need the token string use `.Token`; no other callers today.

---

### Step 4 — Update `IAuthenticationService` + `AuthenticationService`

| Method | Change |
|---|---|
| `SignInAsync` | After issuing JWT: generate opaque refresh token (`CryptographicRandomBytes`), persist `RefreshToken` entity, return real `RefreshToken` in `SignInResponseDto` |
| `RefreshAsync(string refreshToken)` | Validate token exists + not revoked + not expired + `JwtId` matches a valid JWT → revoke old → issue new pair → return `SignInResponseDto` |
| `RevokeAsync(string refreshToken)` | Mark token revoked — used on explicit logout |

> Theft detection: if `GetByTokenAsync` finds an **already-revoked** token, call `RevokeAllForUserAsync(userId)` — entire user's session wiped.

---

### Step 5 — API controller

| File | Action |
|---|---|
| `API/Controllers/V2/AuthController.cs` (new or extend `LoginController`) | `POST /api/auth/refresh` — accepts `{ refreshToken }`, returns new `SignInResponseDto` |
| | `POST /api/auth/revoke` — accepts `{ refreshToken }`, `[Authorize]`, returns `204` |

---

### Step 6 — HTTP scenario file

| File | Action |
|---|---|
| `API/HttpScenarios/auth.http` | Sign-in → capture `refreshToken` → call refresh → call revoke |

---

### Step 7 — Unit tests

| Test class | Cases |
|---|---|
| `AuthenticationServiceTests` (extend existing) | `RefreshAsync` happy path; expired token → exception; revoked token → theft detection (all revoked); wrong `JwtId` → exception |
| `RefreshTokenTests` | `Create` valid; `Revoke` sets flag; cannot revoke twice |

---

### Step 8 — Integration tests

| File | Coverage |
|---|---|
| `IntegrationTests/Identity/IAM/RefreshTokenTests.cs` | Sign-in → refresh → verify new access token valid; refresh with revoked token → `BusinessException`; revoke → refresh fails |

---

## Expiry strategy (to decide before Step 1)

| Option | Recommendation |
|---|---|
| Refresh token TTL | **7 days** (configurable via `AuthOptions`) |
| Access token TTL | Keep existing 120 min or reduce to 15 min — your call |
| Cleanup of expired tokens | Piggyback on existing `JobManagement` infrastructure — add `ExpiredRefreshTokenCleanupJob` (optional, low priority) |

---

## Sequence: how it connects to the existing flow

```
POST /api/auth/signin
  → AuthenticationService.SignInAsync
  → JwtManager.IssueToken  (returns Token + Jti)
  → RefreshTokenRepository.AddAsync
  → SignInResponseDto(accessToken, refreshToken)   ← NOW FILLED

POST /api/auth/refresh
  → AuthenticationService.RefreshAsync
  → RefreshTokenRepository.GetByTokenAsync
  → validate (not revoked, not expired, JwtId match)
  → revoke old token
  → JwtManager.IssueToken (new pair)
  → RefreshTokenRepository.AddAsync (new token)
  → SignInResponseDto(newAccessToken, newRefreshToken)

POST /api/auth/revoke
  → AuthenticationService.RevokeAsync
  → RefreshTokenRepository.GetByTokenAsync
  → RefreshToken.Revoke()
  → save
```

---

## What this unlocks for future IAM work

| Future item | How refresh token enables it |
|---|---|
| `SessionId` support | Add nullable `SessionId Guid?` column in next migration; stamp it into JWT claims; "active devices" backoffice view reads by `SessionId` |
| Token family tracking | Add `FamilyId` column (same Guid across rotations) — enables revoke-entire-lineage on theft without touching all user tokens |
| IAM atomic switch (Step 2) | Integration tests for `AuthenticationService` should cover the full sign-in + refresh + revoke cycle — write them together |
