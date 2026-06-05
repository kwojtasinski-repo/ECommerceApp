# Flow: IAM Refresh Token

> Domain: Customers (Identity and Access)
> Status: Draft
> Last verified: 2026-06-05
> Governing ADR: [docs/adr/0019/README.md](docs/adr/0019/README.md)
> Related roadmap context: [docs/roadmap/iam-refresh-token.md](docs/roadmap/iam-refresh-token.md)

---

## Purpose

Keep authenticated customer sessions secure and continuous by allowing controlled token renewal, one-time token rotation, and explicit session revocation when requested or when misuse is detected.

---

## Scope

### What this spec covers

- Refresh token lifecycle after a successful sign-in.
- Renewal attempt outcomes (success, invalid, expired, revoked, suspected theft).
- Rotation behavior: previous refresh token becomes unusable after successful refresh.
- Explicit revocation of a refresh token by an authenticated actor.
- Terminal outcomes for a single refresh token and for a single refresh attempt.

### What this spec does NOT cover

- Initial credential sign-in and credential verification rules.
- UI navigation, endpoint shape, transport protocol, or payload contracts.
- Storage technology or persistence schema.
- Authorization policy design outside token renewal and revocation.
- Long-term archival or operational cleanup scheduling.

---

## Glossary

| Term | Meaning in this flow |
|---|---|
| Refresh token | Long-lived credential used to request a new access session |
| Access token | Short-lived credential used to access protected operations |
| Rotation | Security step where old refresh token is revoked and a new token pair is issued |
| Revocation | Explicit invalidation of a refresh token before natural expiry |
| Reuse detection | Detection that a revoked token is used again, treated as compromise signal |
| Token family | Sequence of refresh tokens created by repeated successful rotations |
| Terminal state | End state from which this token or this attempt does not proceed further |

---

## Actors

- Customer: requests token renewal to continue a session.
- Authenticated customer session: requests explicit token revocation (logout-like intent).
- Identity and Access system: validates token state, applies rotation, and enforces security response.
- Security control logic: triggers broader session invalidation on suspicious token reuse.

---

## Entry conditions

All must be true to start a refresh attempt:

- A refresh token value is presented.
- The system can locate token state for validation, or determine it is unknown.
- Token renewal is requested within the Identity and Access workflow context.

Additional condition for explicit revocation:

- Caller is already authenticated and requests revocation of a specific refresh token.

---

## Invariants and assumptions

- A refresh token is single-use for successful renewal.
- Successful rotation always invalidates the previous refresh token.
- Expired tokens cannot be renewed.
- Explicitly revoked tokens cannot be renewed.
- Reuse of a revoked token is treated as a potential theft event.
- Security handling on theft suspicion prioritizes account/session safety over continuity.

---

## States

| State | Description | Terminal? |
|---|---|---|
| Active | Token is issued, not expired, not revoked, and eligible for one renewal attempt | No |
| RenewalInProgress | System is validating and processing a renewal request | No |
| Rotated | Renewal succeeded; old token is revoked and replaced by a new token pair | Yes |
| RevokedByUser | Token was explicitly revoked by user intent and is no longer valid | Yes |
| Expired | Token lifetime elapsed before successful renewal | Yes |
| RejectedInvalid | Presented token is unknown or structurally unacceptable for renewal | Yes |
| RejectedRevokedReuse | Presented token was already revoked and reused | Yes |
| SecurityContainment | Suspicious reuse triggered broader session invalidation for the user | Yes |

Exhaustiveness note: every refresh-token path ends in exactly one of the terminal states above for a given token instance or attempt.

---

## Events

- TokenIssued
- RenewalRequested
- TokenValidated
- TokenInvalidDetected
- TokenExpiredDetected
- TokenAlreadyRevokedDetected
- RotationCompleted
- UserRevocationRequested
- RevocationCompleted
- RevokedTokenReuseDetected
- SecurityContainmentTriggered

---

## Transition rules

| From state | Event | Guard condition | To state | Notes |
|---|---|---|---|---|
| Active | RenewalRequested | Renewal requested with this token | RenewalInProgress | Start renewal evaluation |
| RenewalInProgress | TokenValidated | Token is active and within lifetime | Rotated | Old token revoked; new pair issued |
| RenewalInProgress | TokenInvalidDetected | Token not found or invalid for renewal | RejectedInvalid | Renewal denied |
| RenewalInProgress | TokenExpiredDetected | Token expiry time already passed | Expired | Renewal denied |
| RenewalInProgress | TokenAlreadyRevokedDetected | Token already revoked before request | RejectedRevokedReuse | Renewal denied as suspicious |
| RejectedRevokedReuse | SecurityContainmentTriggered | Reuse classified as compromise signal | SecurityContainment | User-wide containment executed |
| Active | UserRevocationRequested | Authenticated actor requests revoke | RevokedByUser | Explicit revoke path |
| RevokedByUser | RenewalRequested | Any later renewal attempt | RejectedRevokedReuse | Revoked token remains invalid |
| Expired | RenewalRequested | Any later renewal attempt | Expired | Expiry is terminal for this token |
| Rotated | RenewalRequested | Old token reused after rotation | RejectedRevokedReuse | Single-use guarantee enforced |

---

## Business rules

| ID | Rule |
|---|---|
| BR-001 | Renewal is allowed only for a token currently in Active state. |
| BR-002 | Every successful renewal must rotate credentials: old refresh token becomes revoked and a new token pair is returned. |
| BR-003 | A token in Expired state must never produce new credentials. |
| BR-004 | A token in RevokedByUser state must never produce new credentials. |
| BR-005 | If a revoked token is presented again, the attempt must be rejected and treated as a security signal. |
| BR-006 | On revoked-token reuse, the system must trigger containment by invalidating all refresh-token sessions for that user. |
| BR-007 | Renewal failure must be explicit and deterministic: invalid, expired, or revoked-reuse outcome must be distinguishable at business level. |
| BR-008 | Explicit revocation is idempotent at business intent level: once revoked, the token remains unusable regardless of repeated revoke or renew attempts. |
| BR-009 | Rotation must preserve user continuity by returning a fresh access token and a fresh refresh token in the same successful outcome. |
| BR-010 | This flow governs session continuity and session safety only; credential verification belongs to sign-in flow outside this specification. |

---

## Edge cases

- Concurrent renewals with the same token: only one attempt may succeed; later attempts must resolve to revoked-reuse rejection.
- Reuse of a just-rotated old token: must be treated as suspicious and trigger containment.
- Renewal request arrives at expiry boundary time: outcome must be deterministic and never partially successful.
- Explicit revocation followed immediately by renewal with same token: renewal must fail as revoked.
- Unknown token presented repeatedly: remains invalid and does not create session state.
- Containment event should prioritize security consistency over preserving any existing token family continuity.

---

## Example scenarios

### Happy path - successful renewal with rotation

1. Customer holds an Active refresh token.
2. Customer requests renewal.
3. System validates token as active and not expired.
4. System revokes old token and issues new token pair.
5. Attempt ends in Rotated.

### Failure path - expired token

1. Customer presents a refresh token after its lifetime has elapsed.
2. System starts renewal evaluation.
3. System detects token expiry.
4. Renewal is denied with explicit expiry outcome.
5. Attempt ends in Expired.

### Failure path - revoked token reuse and containment

1. A refresh token was previously revoked (by rotation or explicit revocation).
2. The same token is presented again for renewal.
3. System classifies this as revoked-token reuse.
4. Renewal is denied and security containment is triggered for the user.
5. Attempt ends in SecurityContainment.
