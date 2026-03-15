---
name: create-http-scenario
description: >
  Scaffold a .http file for testing REST API endpoints (VS/VS Code REST Client format).
  Works for any endpoint style in this API.
  Includes variable declarations, JWT login, and CRUD request sections.
argument-hint: "<controller-or-bc-name>"
---

# Create HTTP Scenario

Generate a `.http` file for manual API testing.

## File placement

`ECommerceApp.API/HttpScenarios/{{name}}.http`

Naming conventions:

- Use descriptive names (e.g., `catalog.http`, `orders.http`, `checkout.http`)
- You can keep existing naming conventions already present in the repo

## Template A — Resource endpoints

```http
### ============================================================
### {{BcName}} V2
### Base URL: https://localhost:49609
###
### Auth: Bearer token required for all endpoints (unless noted)
### ============================================================

@baseUrl = https://localhost:49609
@token = YOUR_JWT_TOKEN
@contentType = application/json
@routePrefix = /api/{{resource}}

# ============================================================
# AUTH (copy token into @token above)
# ============================================================

### Login as admin
POST {{baseUrl}}/api/Login
Content-Type: {{contentType}}

{
  "email": "admin@localhost",
  "password": "aDminN@W25!"
}

###

# ============================================================
# {{RESOURCE}} — GET
# ============================================================

### Get all (paginated)
GET {{baseUrl}}{{routePrefix}}?pageSize=10&pageNo=1&searchString=
Authorization: Bearer {{token}}

### Get by ID
GET {{baseUrl}}{{routePrefix}}/1
Authorization: Bearer {{token}}

###

# ============================================================
# {{RESOURCE}} — POST
# ============================================================

### Create
POST {{baseUrl}}{{routePrefix}}
Content-Type: {{contentType}}
Authorization: Bearer {{token}}

{
    "name": "Test {{EntityName}}",
    "description": "Created via HTTP scenario"
}

###

# ============================================================
# {{RESOURCE}} — PUT
# ============================================================

### Update
PUT {{baseUrl}}{{routePrefix}}/1
Content-Type: {{contentType}}
Authorization: Bearer {{token}}

{
    "id": 1,
    "name": "Updated {{EntityName}}",
    "description": "Updated via HTTP scenario"
}

###

# ============================================================
# {{RESOURCE}} — DELETE
# ============================================================

### Delete by ID
DELETE {{baseUrl}}{{routePrefix}}/1
Authorization: Bearer {{token}}
```

## Template B — Custom route endpoints

```http
### ============================================================
### {{ResourceName}} endpoint group
### Base URL: https://localhost:49609
###
### Auth: [Authorize] on class — Bearer token required
### ============================================================

@baseUrl = https://localhost:49609
@token = YOUR_JWT_TOKEN
@contentType = application/json
@routePrefix = /api/{{resource}}

# ============================================================
# AUTH (copy token into @token above)
# ============================================================

### Login
POST {{baseUrl}}/api/Login
Content-Type: {{contentType}}

{
  "email": "admin@localhost",
  "password": "aDminN@W25!"
}

###

# ============================================================
# {{RESOURCE}}
# ============================================================

### Get all
GET {{baseUrl}}{{routePrefix}}
Authorization: Bearer {{token}}

### Get by ID
GET {{baseUrl}}{{routePrefix}}/1
Authorization: Bearer {{token}}

### Get by sub-resource (if applicable)
# GET {{baseUrl}}{{routePrefix}}/by-customer/1
# Authorization: Bearer {{token}}

### Create
POST {{baseUrl}}{{routePrefix}}
Content-Type: {{contentType}}
Authorization: Bearer {{token}}

{
    "name": "Test {{EntityName}}"
}

### Update
PUT {{baseUrl}}{{routePrefix}}/1
Content-Type: {{contentType}}
Authorization: Bearer {{token}}

{
    "id": 1,
    "name": "Updated {{EntityName}}"
}

### Delete
DELETE {{baseUrl}}{{routePrefix}}/1
Authorization: Bearer {{token}}
```

## How to generate

1. **Read the target controller** to discover: route prefix, HTTP methods, action parameters, auth requirements, and DTO shapes.
2. Pick the template based on the endpoint shape (resource CRUD or custom routes).
3. Fill in request bodies matching the actual DTO/ViewModel properties.
4. For endpoints with sub-routes (e.g., `/by-customer/{id}`, `/by-user`), add a dedicated `###` section.
5. For anonymous endpoints, omit the `Authorization` header and note it in the file header comment.

## Conventions

1. Separator: `###` between requests, `# ====` comment blocks for grouping
2. Variables at the top: `@baseUrl`, `@token`, `@contentType`
3. Always include the Login section first
4. Set `@routePrefix` to the controller route (for example: `/api/orders`, `/api/v2/orders`, `/api/storefront`)
5. Include `Authorization: Bearer {{token}}` on all protected endpoints
6. Request bodies use realistic sample data matching the DTO/command shape
7. Group by resource, then by HTTP verb within each resource
8. Comment out optional/sub-resource endpoints with `#` so they're easy to enable
