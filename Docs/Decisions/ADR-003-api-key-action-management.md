# ADR-003: API Key Action Management

**Status:** Pending  
**Date:** 2025-06-01  
**Related concept:** Concept 3 — API Key Action Management

---

## Context

I needed to find a way to manage API key actions, because not all API keys should have the same privileges. This is important for security reasons, as it allows for more granular control over what actions can be performed with each API key. If a key is compromised, the damage it can do is limited by the actions it is allowed to perform.



## Options considered

| Option | What it is | Why it was considered |
|---|---|---|
| Normalized junction table | A separate `api_key_permissions` table with `(api_key_id, action)` rows linking keys to their allowed actions | This was considered because it is the standard relational model for many-to-many relationships — individual permissions can be added or revoked without touching the key row, and both directions of the relationship are queryable |
| Predefined roles assigned to the key | A fixed set of roles (e.g. `ReadOnly`, `ReadWrite`, `Admin`) stored as a single column on the API key record | This was considered because it removes per-key complexity — authorization is a simple enum comparison with no JOIN, and the set of valid permission combinations is constrained by design |


## Decision

The Normalized junction table was chosen. Because it allows for per-key least-privilege enforcement with independent grant/revoke.

## Reasoning

I chose a normalized junction table (api_key_permissions) because it gives each key an independently configurable set of allowed actions — a compromised key's blast radius is bounded by exactly the permissions assigned to it, and permissions can be granted or revoked without modifying the key itself.

## Security implications

The severity of a compromise,is now dependent on what permission the key has. This **can** defend against, a data breach and data corruption, only if the actions are managed responsibly by the user. Though this system can be abused by an attacker, if he manages to update the access of a low privilege key.

## Consequences

This decision will separate the data model of the key itself from to the actions it can perform, which improves the maintainability of the system, while also separating concerns. 

Although it gives positive benefits the security measurements of the system now need to be improved to a greater extend because now privilege escalation becomes a threat. This is why only the key owner (or an admin) can modify a key's permissions, enforced at the authorization layer.

We need to implement a proper cleanup, when a API key is deleted, after a specific time period.

## What this taught me

TODO: not implemented yet

Write 2–4 sentences in your own words. What did implementing this decision
teach you about how secure systems are designed? This is a learning journal,
not just a reference document — the reflection section is not optional.
