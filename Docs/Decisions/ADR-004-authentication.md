# ADR-004: Authentication

**Status:** Pending  
**Date:** 2025-06-02
**Related concept:** Concept 4 — User Authentication

---

## Context

Locksmith manages the full lifecycle of API keys — creating, rotating, and revoking them. That means the management endpoints themselves need to be protected: only authorized users should be able to issue or revoke keys.

## Options considered

| Option | What it is | Why it was considered |
|---|---|---|
| JWT with local credential store | Users register with a username and password. The API hashes and stores the password, then issues a signed JWT on successful login. Every subsequent request presents that token in the `Authorization` header. | The most self-contained option — no external services, full ownership of the auth surface, and JWT is the de-facto standard for stateless API authentication in .NET. |
| OIDC / OAuth 2.0 via external IdP | Delegate identity entirely to a third-party provider (Keycloak, Auth0, Azure AD). Users authenticate with the IdP; the API only validates the resulting JWT. | Eliminates credential storage and gives SSO for free. The right answer in an org that already runs an IdP, or if multi-tenant auth is ever needed. |
| Mutual TLS (mTLS) | Clients present a certificate; the API validates it against a trusted Certificate Authority. No passwords or tokens involved. | Very strong for machine-to-machine communication inside a Docker network. Considered because Locksmith may be called by automated systems rather than humans. |
| Static bearer token (pre-shared secret) | A single secret is configured via environment variable. The API rejects any request that doesn't include it as a `Bearer` token. | The simplest possible implementation — zero user management, zero token logic. Worth considering when Locksmith is called by exactly one trusted service and operational simplicity matters more than per-user identity. |

## Decision

Static bearer token (pre-shared secret).

## Reasoning

Locksmith's management endpoints need to be protected, and at this stage the only caller is a single trusted internal service — not a human user, not multiple independent clients. A static bearer token solves that completely: one secret configured via environment variable, one middleware check, no database, no login endpoint, no token signing keys. Every other option adds complexity that buys nothing given a single caller. JWT with a local credential store introduces a user model and a login flow that are irrelevant for service-to-service calls. OIDC requires an external IdP that doesn't exist yet. mTLS requires certificate infrastructure that would dwarf the application itself. What this choice gives up is per-caller identity and granular revocation — if the token leaks, the only remedy is rotating the secret for everyone at once. The decision should be revisited the moment a second independent caller needs access, or if per-client audit logging becomes a requirement; at that point per-client API keys (the same model Locksmith issues to its own consumers) is the natural upgrade path.

## Security implications

This decision defends against unauthenticated access to the key management surface. Without a valid token in the `Authorization: Bearer` header, every request is rejected with a 401 before any handler runs. The attack surface it opens is credential exposure: the token is a single high-value secret, and if it appears in logs, error messages, environment dumps, or source control, all access is compromised with no per-caller revocation available. Two implementation details are load-bearing: the token comparison must use `CryptographicOperations.FixedTimeEquals` (not `==`) to prevent timing attacks that allow an attacker to guess the secret one character at a time; and the secret must be injected via a secrets manager or environment variable at deploy time, never committed to source control.

## Consequences

Simpler to implement and operate than any alternative: there is no user model to design, no token issuance endpoint to secure, and no external service dependency. Rotation is operationally straightforward but requires coordinating both caller and server simultaneously since there is no grace period or per-client key. Scaling to multiple independent callers is not possible without a full redesign — the single-secret model has no concept of caller identity. This creates an obligation to revisit authentication before onboarding a second consumer.

## What this taught me

Write 2–4 sentences in your own words. What did implementing this decision
teach you about how secure systems are designed? This is a learning journal,
not just a reference document — the reflection section is not optional.
