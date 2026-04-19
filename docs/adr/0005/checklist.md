## Conformance checklist

- [ ] All `UserProfile` and `Address` properties use `private set` (record: init-only via constructor)
- [x] `UserProfile.Create(...)` is `static`, returns `UserProfile` (plain return — event tuple removed; see Risks & mitigations)
- [ ] `UserProfile.cs` has a `private UserProfile()` parameterless constructor for EF Core
- [ ] `UserProfile.cs` lives under `Domain/AccountProfile/` (no group subfolder)
- [ ] No `ICollection<Order>`, `ICollection<Payment>` or other cross-BC navigation in `UserProfile.cs`
- [ ] No `ApplicationUser` navigation property — `string UserId` only
- [ ] `Address` has no repository interface — all mutations go through `UserProfile` aggregate methods
- [ ] `UserProfileDbContext` uses schema `"profile"`
- [ ] `UserProfileService` is `internal sealed`
