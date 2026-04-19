## Conformance checklist

- [ ] `ApplicationUser` class exists only under `Domain/Identity/IAM/` — not in `Domain/Model/`
- [ ] No BC outside Identity/IAM has a navigation property typed `ApplicationUser`
- [ ] `Order.cs` (and all other domain models) reference users via `string UserId` only
- [ ] `IamDbContext` uses EF Core schema `"iam"`
- [ ] `LoginController` and `UserManagementController` inject only `Application.Identity.IAM` interfaces
- [ ] Legacy `IUserService`, `AuthenticationService` (Application/Services/) files deleted after switch
- [ ] `UseIamStore` feature flag removed after switch
- [ ] JWT token issued from `IJwtManager` — no hardcoded signing keys
- [ ] Google OAuth client ID / secret loaded from configuration — not hardcoded
