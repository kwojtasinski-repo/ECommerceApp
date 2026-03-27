# Roadmap: Identity/IAM — Refresh Token Feature

> ADR: [ADR-0019](../adr/0019-identity-iam-bc-design.md) — Identity/IAM BC Design (amendment pending — see note below)
> Status: ✅ Complete — Steps 1–8 ✅ done (2026-05-28); `ExpiredRefreshTokenCleanupJob` deferred (optional, low priority)
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
| `Application/Identity/IAM/DTOs/SignInResponseDto.cs` | `record SignInResponseDto(string AccessToken, string RefreshToken)` — ✅ `RefreshToken` now populated |
| `Infrastructure/Identity/IAM/Auth/JwtManager.cs` | Issues `Jti` claim (`Guid.NewGuid()`) on every token — ✅ returns `(Token, Jti)` tuple |
| `Application/Identity/IAM/Services/AuthenticationService.cs` | ✅ `SignInAsync` persists `RefreshToken` entity; `RefreshAsync` + `RevokeAsync` implemented |

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

### Step 1 — Domain entity + repository interface ✅ Done

| File | Status |
|---|---|
| `Domain/Identity/IAM/RefreshToken.cs` | ✅ `Create(userId, token, jwtId, expiresAt)`, `Revoke()` method |
| `Domain/Identity/IAM/IRefreshTokenRepository.cs` | ✅ `AddAsync`, `GetByTokenAsync`, `RevokeAllForUserAsync` |

---

### Step 2 — EF Core configuration + migration ✅ Done

| File | Status |
|---|---|
| `Infrastructure/Identity/IAM/Configurations/RefreshTokenConfiguration.cs` | ✅ Map to `iam.RefreshTokens`; indexes in place |
| `Infrastructure/Identity/IAM/IamDbContext.cs` | ✅ `DbSet<RefreshToken>` added |
| `Infrastructure/Identity/IAM/Repositories/RefreshTokenRepository.cs` | ✅ Implemented |
| Migration `20260326002201_AddRefreshTokensTable` | ✅ Generated — **pending approval per migration policy** |

---

### Step 3 — Update `IJwtManager` + `JwtManager` ✅ Done

| File | Status |
|---|---|
| `Application/Interfaces/IJwtManager.cs` | ✅ Returns `(string Token, string Jti)` tuple |
| `Infrastructure/Identity/IAM/Auth/JwtManager.cs` | ✅ Returns `Jti` alongside token |

> **Non-breaking**: callers that only need the token string use `.Token`; no other callers today.

---

### Step 4 — Update `IAuthenticationService` + `AuthenticationService` ✅ Done

| Method | Status |
|---|---|
| `SignInAsync` | ✅ Persists `RefreshToken` entity, returns real `RefreshToken` in `SignInResponseDto` |
| `RefreshAsync(string refreshToken)` | ✅ Validate + rotate + return new pair |
| `RevokeAsync(string refreshToken)` | ✅ Mark token revoked |

> Theft detection: if `GetByTokenAsync` finds an **already-revoked** token, calls `RevokeAllForUserAsync(userId)` — entire user's session wiped.

---

### Step 5 — API controller ✅ Done

| File | Status |
|---|---|
| `API/Controllers/V2/AuthController.cs` ✅ Created | `[AllowAnonymous] POST /api/auth/refresh` → `SignInResponseDto`; `[Authorize] POST /api/auth/revoke` → 204 |
| `Application/Identity/IAM/DTOs/RefreshTokenDto.cs` ✅ Created | `record RefreshTokenDto(string RefreshToken)` — shared request body DTO |

---

### Step 6 — HTTP scenario file ✅ Done

| File | Status |
|---|---|
| `API/HttpScenarios/auth.http` ✅ Created | Login → Refresh (anonymous) → Revoke (authorized) → stale-token re-use scenario |

---

### Step 7 — Unit tests ✅ Done

| Test class | Status |
|---|---|
| `AuthenticationServiceTests` (extended) | ✅ `RefreshAsync` happy path; expired token → exception; revoked token → theft detection; wrong `JwtId` → exception |
| `RefreshTokenTests` | ✅ `Create` valid; `Revoke` sets flag; cannot revoke twice |

---

### Step 8 — Integration tests ✅ Done

| File | Status |
|---|---|
| `IntegrationTests/Identity/IAM/RefreshTokenIntegrationTests.cs` ✅ Created | 4 scenarios: valid refresh → new pair; invalid token → `BusinessException`; rotation reuse (theft detection) → `BusinessException`; revoke-then-refresh → `BusinessException` |

> **Infrastructure fixes required to support JWT in the test environment:**
> - `IntegrationTests/Common/BcWebApplicationFactory.cs` — `ConfigureAppConfiguration` injects `Jwt:Key/Issuer/RefreshTokenTtlDays`; `JwtManager` is a singleton that reads `AuthOptions.Key` in its constructor — the test host had no `Jwt` section in any appsettings.
> - `IntegrationTests/Common/HttpContextAccessorTest.cs` — now accepts `IServiceProvider` via constructor and sets `HttpContext.RequestServices`; required by `SignInManager.PasswordSignInAsync` → `context.SignInAsync(...)` chain.

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
