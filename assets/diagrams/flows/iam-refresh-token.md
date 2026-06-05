# IAM Refresh Token Flow

High-level token rotation and revoke behavior.
Detailed business rules will be maintained in docs/specifications.

```mermaid
graph TD
    A(User signs in) --> B(Issue access token and refresh token)
    B --> C(Store refresh token)
    C --> D(User calls refresh endpoint)
    D --> E{Refresh token valid and active}
    E -->|No| F([Reject refresh])
    E -->|Yes| G(Revoke old refresh token)
    G --> H(Issue new token pair)
    H --> I(Store new refresh token)
    I --> J([Refresh success])
    C --> K(User calls revoke endpoint)
    K --> L(Revoke token)
    L --> M([Session revoked])
```

References:
- ../../../docs/specifications/iam-refresh-token.md
- docs/roadmap/iam-refresh-token.md
- docs/adr/0019/0019-identity-iam-bc-design.md
