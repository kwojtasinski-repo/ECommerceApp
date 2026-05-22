# Release Notes

## Version 2.0

Major rewrite of the order processing subsystem with improved performance,
stricter validation, and support for multi-currency transactions.
The API is not backward compatible with version 1.x.
Migration guide is available in MIGRATION.md.
See the upgrade checklist in the appendix before proceeding.
All data must be backed up before starting the upgrade process.

## See Also

See MIGRATION.md for step-by-step upgrade instructions.

## Breaking Changes

The OrderService interface has changed significantly in this release.
The CreateOrder method now requires a UserId parameter for audit logging.
The UpdateOrder method was split into UpdateOrderDetails and UpdateOrderStatus.
Existing implementations must be updated before upgrading to version 2.0.
Automated migration scripts are provided for the most common patterns.
Review the full list of breaking changes in CHANGELOG.md before migrating.

## Deprecated APIs

The following APIs are deprecated and will be removed in version 3.0:

- LegacyOrderService (use OrderService instead)
- OldCatalogRepository (use CatalogRepository instead)
- GetAllOrders without pagination (use the paged overload instead)
- DirectDatabaseAccess helpers (use repositories instead)

Plan your migration away from these APIs before the next major release.
