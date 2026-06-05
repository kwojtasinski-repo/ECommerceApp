# IAM Refresh Token Flow

Current implementation flow (service + controller), without synthetic event naming.

```mermaid
graph TD
    A(User calls refresh endpoint) --> B(Get token by value)
    B --> C{Token found}
    C -->|No| D([BusinessException: Invalid refresh token])
    C -->|Yes| E{Token revoked}
    E -->|Yes| F(Revoke all user tokens)
    F --> G([BusinessException: revoked token reuse])
    E -->|No| H{Token expired}
    H -->|Yes| I([BusinessException: Refresh token has expired])
    H -->|No| J(Revoke current token)
    J --> K(Issue new JWT)
    K --> L(Create and persist new refresh token)
    L --> M([Return new token pair])
```

References:

- ../../../docs/specifications/iam-refresh-token.md
- ECommerceApp.Application/Identity/IAM/Services/AuthenticationService.cs
- ECommerceApp.API/Controllers/IAM/AuthController.cs
