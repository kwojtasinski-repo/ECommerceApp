## Migration plan

1. New `UserProfileDbContext` migration creates `profile.UserProfiles` and `profile.Addresses` tables.
2. Data migration script (separate PR, approval required):
   - For each existing `Customer`, insert a row into `profile.UserProfiles`
     mapping `FirstName`, `LastName`, `IsCompany`, `NIP`, `CompanyName`, `UserId`.
   - Populate `Email` from the most recent `ContactDetail` row where `ContactDetailType.Name = 'Email'`.
   - Populate `PhoneNumber` from the most recent `ContactDetail` row where `ContactDetailType.Name IN ('Phone', 'Mobile')`.
   - Insert existing `Address` rows into `profile.Addresses`.
3. Controller switch: update `CustomerController`, `AddressController`, `ContactDetailController`
   to inject `IUserProfileService`.
4. Remove old DI registrations for `ICustomerService`, `IAddressService`, `IContactDetailService`,
   `IContactDetailTypeService` after all tests pass.
