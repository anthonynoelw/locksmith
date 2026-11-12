# ADR-003: API Key Action Management

**Status:** Accepted  
**Date:** 2026-06-01  
**Related concept:** [Concept 003 — API Key Action Management](../Concepts/concept-003-api-key-action-management.md)

---

## Context

I needed to find a way to manage API key actions, because not all API keys should have the same privileges. This is important for security reasons, as it allows for more granular control over what actions can be performed with each API key. If a key is compromised, the damage it can do is limited by the actions it is allowed to perform.



## Options considered

| Option | What it is | Why it was considered |
|---|---|---|
| Normalized junction table | A separate permissions table (implemented as `ApiKeyActions`) with `(ApiKeyId, Action)` rows linking keys to their allowed actions | This was considered because it is the standard relational model for many-to-many relationships — individual permissions can be added or revoked without touching the key row, and both directions of the relationship are queryable |
| Predefined roles assigned to the key | A fixed set of roles (e.g. `ReadOnly`, `ReadWrite`, `Admin`) stored as a single column on the API key record | This was considered because it removes per-key complexity — authorization is a simple enum comparison with no JOIN, and the set of valid permission combinations is constrained by design |


## Decision

The normalized junction table was chosen, because it allows for per-key least-privilege enforcement with independent grant/revoke.

## Reasoning

I chose a normalized junction table (`ApiKeyActions`) because it gives each key an independently configurable set of allowed actions — a compromised key's blast radius is bounded by exactly the permissions assigned to it, and permissions can be granted or revoked without modifying the key itself.

## Security implications

The severity of a compromise is now dependent on what permissions the key has. This **can** defend against a data breach and data corruption, but only if the actions are managed responsibly by the user. The system can still be abused by an attacker who manages to update the access of a low-privilege key.

## Consequences

This decision separates the data model of the key itself from the actions it can perform, which improves the maintainability of the system while also separating concerns. 

Despite these benefits, the security of the system now needs to be held to a higher standard, because privilege escalation becomes a threat. Modifying a key's permissions therefore requires both the management bearer token and that key's idempotency key (see Implementation notes).

A cleanup process for deleted API keys after a retention period is still needed.

## Implementation notes (v1.1.1)

How the decision was built, and where it differs from the original plan:

- **Table:** `ApiKeyActions` (`Id`, `ApiKeyId`, `Action`, `CreatedAt`, `CreatedBy`, `DeletedAt`). Actions are `Read`, `Write`, `Delete`, `Execute`. Granting inserts a row; revoking soft-deletes it, so the full grant/revoke history is kept.
- **Uniqueness:** a partial unique index on `(ApiKeyId, Action)` where `DeletedAt IS NULL` allows at most one *active* grant per action, and lets a revoked action be granted again.
- **Who can change permissions:** there is no key-owner or admin concept. Any caller holding the management bearer token **and** the target key's idempotency key can list, replace, grant, or revoke actions. With a single trusted caller (see [ADR-004](ADR-004-authentication.md)) that caller is effectively the admin; per-caller authorization would need the authentication upgrade described there.
- **Enforcement:** Locksmith stores and reports permissions; it does not itself enforce them on downstream requests — consuming services check the granted actions.
- **Cleanup of deleted keys:** not implemented. Deleted keys' rows are kept indefinitely (archival is a recommendation in [database.md](../Architecture/database.md#performance-considerations)).
