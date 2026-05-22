# Service Design

This document records key architectural decisions about service design.
Services should be autonomous, have clear boundaries, and communicate
via well-defined interfaces. Dependencies must flow inward.

## API Design

APIs should be RESTful and consistent across all bounded contexts.
Use proper HTTP methods: GET for reads, POST for creates,
PUT for full updates, PATCH for partial updates, DELETE for removes.
Always version your API endpoints before publishing them.
Deprecate old versions with at least six months notice.

### Resource Naming

Resources use plural nouns: /orders, /items, /customers.
Use kebab-case for multi-word resources: /order-items.
Nested resources indicate ownership: /orders/{id}/items.
Avoid deeply nested URLs beyond two levels.

#### Versioning Strategy

API versions are embedded in the URL path: /api/v1/orders.
Never break existing versions — only add new ones.
Deprecation requires a minimum six-month notice period.
All breaking changes require a major version bump.
Supporting old versions costs more than you think.
Plan your deprecation strategy from day one of the API.

#### Status Codes

Use 200 for successful GET and PUT.
Use 201 for successful POST when a resource is created.
Use 204 for successful DELETE with no content body.
Use 400 for client validation errors with a details array.
Use 404 when the requested resource does not exist.
Use 409 for conflicts such as duplicate resource creation.
Use 500 only for unexpected server errors, never for business errors.

## Data Access

All data access goes through repository interfaces defined in the domain layer.
The domain layer defines repository contracts; infrastructure implements them.
Never access the database directly from application services.

### Repository Pattern

Repositories abstract the persistence mechanism from business logic.
They work with domain aggregates, not data transfer objects.
A repository must never return partially initialised aggregates.

#### Unit of Work

Changes are committed atomically via the unit of work pattern.
No partial saves — either everything commits or nothing does.
The unit of work scope is typically per HTTP request or per command.
Nested units of work are not supported; avoid them by design.

#### Connection Management

Connection pooling is handled exclusively by the infrastructure layer.
Each request gets its own unit of work scope through dependency injection.
Never open a connection in domain or application layer code.
