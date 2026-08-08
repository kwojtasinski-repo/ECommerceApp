# ECommerceApp

Simple e-commerce application with Web MVC + REST API, built around bounded contexts and ADR-driven architecture.

## Admin User

- login: admin@localhost
- password: aDminN@W25!

## Quick Start

1. Apply database migrations.
2. Configure Google OAuth in Web app user secrets:

```bash
dotnet user-secrets set "Authentication:Google:ClientId" "<ClientIdFromGoogle>" --project ECommerceApp.Web
dotnet user-secrets set "Authentication:Google:ClientSecret" "<ClientSecretFromGoogle>" --project ECommerceApp.Web
```

Google OAuth console: https://console.cloud.google.com/ (OAuth2 credentials).

## Technology Stack

- .NET 10
- ASP.NET Core MVC + Web API
- EF Core + MSSQL
- FluentValidation, AutoMapper
- xUnit, Moq
- Bootstrap, jQuery, RequireJS

## Visual Overview

Short visual/diagram assets live in [assets/README.md](assets/README.md).

- Screens: [assets/screens/README.md](assets/screens/README.md)
- Flow diagrams: [assets/diagrams/README.md](assets/diagrams/README.md)

## Architecture And Decisions

- Architecture entry: [docs/README.md](docs/README.md)
- Bounded contexts: [docs/architecture/bounded-context-map.md](docs/architecture/bounded-context-map.md)
- ADR index: [docs/adr](docs/adr)
- Roadmap: [docs/roadmap/README.md](docs/roadmap/README.md)
- Structural knowledge graph: [ADR-0031](docs/adr/0031/0031-structural-knowledge-graph.md) — a Neo4j graph generated from this repo's source by [`tools/kg/kg-codegen`](tools/kg/kg-codegen/README.md) and queried through the read-only MCP server [`tools/kg/kg-mcp`](tools/kg/kg-mcp/README.md). Answers structural questions (what breaks if I change this message, which module owns this action) from code rather than from prose about code. Tool reference: [docs/reference/kg-mcp-tools.md](docs/reference/kg-mcp-tools.md).
- Workflow specifications index: [docs/specifications/README.md](docs/specifications/README.md)
- Orders checkout spec: [docs/specifications/orders-checkout.md](docs/specifications/orders-checkout.md)
- Payments lifecycle spec: [docs/specifications/payments-lifecycle.md](docs/specifications/payments-lifecycle.md)
- Inventory reservation release spec: [docs/specifications/inventory-reservation-release.md](docs/specifications/inventory-reservation-release.md)
- Coupons apply-revert spec: [docs/specifications/coupons-apply-revert.md](docs/specifications/coupons-apply-revert.md)
- IAM refresh token spec: [docs/specifications/iam-refresh-token.md](docs/specifications/iam-refresh-token.md)

## Project Status

Project is active and continuously refined; current progress and switches are tracked in the roadmap and ADRs.
