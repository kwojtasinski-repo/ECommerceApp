# ADR-0005: AccountProfile BC — UserProfile Aggregate with Owned Address and Direct Contact Fields

## Status
Accepted

## Date
2026-02-23

## Context
The existing `Customer` domain model is fully anemic: all public setters, navigation properties to
`ApplicationUser` (IAM leak), and cross-BC navigation collections to `Order`, `Payment`, and `Refund`.
`Address` and `ContactDetail` are separate entities with their own repositories, queried independently
and joined via `Customer.UserId` traversal chains.

During BC analysis and design, the following problems were identified:

1. **Naming conflict** — the class `AccountProfile` in namespace `ECommerceApp.Domain.AccountProfile`
   would collide with the namespace itself, requiring `global::` qualifiers throughout.
2. **Unnecessary flexibility in ContactDetail** — the existing `ContactDetailType` system (email, phone, fax, …)
   adds a level of indirection that is never used: e-commerce ordering always requires both email and phone.
3. **Over-engineered aggregation** — `Address` and `ContactDetail` as independent entities with their own
   repositories and services duplicates access patterns without adding domain value; they have no
   lifecycle outside of their owner.
4. **Group folder redundancy** — the intermediate `Profiles/` group folder adds a navigation layer for
   a group that contains a single BC.

## Decision

### 1. Aggregate class renamed to `UserProfile`
The aggregate root in namespace `ECommerceApp.Domain.AccountProfile` is named `UserProfile`.
This eliminates the namespace/class collision and expresses the domain concept clearly.

### 2. Group folder removed
The folder path is `Domain/AccountProfile/` (not `Domain/Profiles/AccountProfile/`).
A one-BC group does not warrant an extra hierarchy level.

### 3. `Address` becomes an owned entity (EF Core `OwnsMany`)
`Address` is encapsulated inside `UserProfile` with no separate repository.
All address operations (add, update, remove) go through the `UserProfile` aggregate methods.
EF Core `OwnsMany` maps addresses to `profile.Addresses` with a shadow FK `UserProfileId`
and cascade delete on the owner. The backing field `_addresses` is configured via
`UsePropertyAccessMode(PropertyAccessMode.Field)`.

### 4. `ContactDetail` and `ContactDetailType` eliminated
Direct `Email` and `PhoneNumber` properties replace the flexible typed contact detail system.
Rationale: e-commerce ordering always requires both; typed extensibility adds complexity with
no current business value.

### 5. Single service: `IUserProfileService`
All profile and address operations are exposed through one service interface, reflecting the
single aggregate boundary. No separate `IAddressService` or `IContactDetailService`.

### 6. Schema: `profile.*`
Own `UserProfileDbContext : DbContext` with schema `"profile"`.
Tables: `profile.UserProfiles`, `profile.Addresses`.

`UserId` carries a non-unique index (`HasIndex(p => p.UserId)` without `IsUnique()`). One `ApplicationUser` may own multiple `UserProfile` rows; the unique constraint was intentionally omitted to support that scenario.

## Consequences

### Positive
- No `global::` qualifiers — clean, readable code throughout.
- Fewer files: 1 repository interface, 1 repository, 1 service instead of 4 each.
- Address mutations go through the aggregate — invariants enforced in one place.
- `ContactDetailType` CRUD removed entirely — less code, less surface area.
- BC folder structure matches ADR-0003 without redundant group nesting.

### Negative
- `Address.Id` is exposed on the domain entity (needed for service-layer identification within
  the `OwnsMany` collection). Strictly, a pure value object would have no identity. This is an
  accepted pragmatic compromise.
- Address updates require loading the full `UserProfile` (tracked context), not a lightweight
  point update.
- Existing `ContactDetail`/`ContactDetailType` data must be migrated to `Email`/`PhoneNumber`
  columns during the atomic switch.

### Risks & mitigations
- **EF Core OwnsMany + private backing field** — verified to work in EF Core 7 via
  `Navigation().HasField("_addresses").UsePropertyAccessMode(PropertyAccessMode.Field)`.
  Build is green and unit tests pass.
- **`Address.Id` EF Core sentinel** — `AddressId` is a reference-type record; EF Core's sentinel
  for `ValueGeneratedOnAdd()` on a reference-type property is `null`. Initializing the backing
  field to `new AddressId(0)` is non-null, so EF treats every new `Address` as an already-existing
  entity and emits `UPDATE WHERE Id = 0` instead of `INSERT`. Zero rows affected →
  `DbUpdateConcurrencyException`. **Fix**: initialize `Address.Id` to `default!` (null) so EF
  correctly recognises the sentinel and generates `INSERT`. See also ADR-0006 Risks.
- **`UserProfile.Create()` domain event tuple removed** — the original design returned
  `(UserProfile, UserProfileCreated)` to support out-of-band event dispatch. The tuple was
  removed because the in-process `InMemoryMessageBroker` (ADR-0010) is the authoritative
  cross-BC channel; the service layer publishes events explicitly after persistence.
- **Data migration** — the atomic switch from old Customer BC will require a data migration
  script to populate `Email` and `PhoneNumber` from the most common `ContactDetail` rows.
  This will be handled as a separate migration PR with explicit reviewer sign-off per
  `migration-policy.md`.

## Alternatives considered

- **Keep `AccountProfile` as the class name** — rejected due to C# namespace/class collision
  requiring `global::` qualifiers in every consuming file.
- **`ProfileData` as the class name** — rejected; the `Data` suffix implies a DTO, not a
  domain aggregate.
- **Keep `Address` as a standalone entity with its own repository** — rejected; addresses have
  no lifecycle outside the profile and independent address CRUD is an anti-pattern for an
  aggregate that owns them.
- **Keep `ContactDetail` + `ContactDetailType`** — rejected; typed contact details add
  indirection for a use-case (email + phone for ordering) that is always fixed in this domain.
- **Keep `Profiles/` group folder** — rejected; a group with a single BC does not need an
  intermediate folder level.
