# ADR-001: API Key Lifecycle

**Status:** Accepted  
**Date:** 2026-05-31  
**Related concept:** [Concept 001 — API Key Lifecycle](../Concepts/concept-001-api-key-lifecycle.md)

---

## Context

In the start of the project I needed to map out what phases each API key could have and what actions needed to be performed in each phase. 
I needed to figure out how the data of the API key lifecycle should be stored.

## Options considered

| Option | What it is | Why it was considered |
|---|---|---|
| One Table with Status Column | The key and the current status of the API key will be stored in a single table with a status column | This was considered because it is simple and easy to understand |
| One KeyHash Table and One Status Table | The key and the current status of the API key will be stored in separate tables, with the key hash in the key table and the status in the status table | This was considered because it is more scalable, allows for more complex status tracking as well as easier auditing |

## Decision

I decided to use the separate table approach because of the ability to track the history of the API key status changes and the ability to easily query the API key status changes.

## Reasoning

The separate table approach was better because it allows for better auditability and traceability of the API key status changes than the one-table option. The status table holds all status changes for an API key and is append-only. Soft deletes fit naturally into this model — deleting a key sets a `DeletedAt` timestamp on its status, action, and idempotency-key rows instead of removing them, while the key row and the full status history are preserved intact. This is important for security auditing and compliance reasons. I lose some simplicity in implementation, maintainability, and performance. I may consider the one-table approach if performance is not acceptable due to the additional data.

## Security implications

This separation of concerns allows us to track the change of access from an API key, which can be used to detect suspicious activity and prevent unauthorized access. The append-only nature of the status table ensures that the history of API key status changes cannot be rewritten through the application, and soft deletes extend that guarantee to deletion events — the moment a key was retired is permanently on record. This allows an investigation to be conducted more easily and quickly. The soft delete approach also means that any system checking key validity simply queries the latest non-deleted status entry; a deleted key has none left and is rejected, without any physical row removal that could otherwise erase forensic evidence.

## Consequences

Telemetry of status changes, including deletions, can therefore be more easily implemented and maintained, but the maintenance of the status table adds some complexity to the implementation. Queries for "active" keys must filter on the latest status entry rather than on the absence of a row, which adds a small but consistent overhead to key-validation hot paths.

## Implementation notes (v1.1.1)

How the decision was built, and where it differs from the original plan:

- **Tables:** `ApiKeys` (key row, never modified after insert) and `ApiKeyStatuses` (one row per transition). A transition soft-deletes the current status row and inserts a new one; the current status is the newest row with no `DeletedAt`.
- **States:** `Inactive` (default at creation), `Active`, `Revoked`, `Expired`. There is no `Deleted` status — deletion is expressed through `DeletedAt` on the related rows.
- **Transition guard:** a terminal-state check, not a full transition matrix — any status may be set unless the current one is `Revoked` or `Expired` (`409`).
- **Append-only enforcement:** application-layer only (`AppDbContext.SaveChanges` guard on all four `IAppendOnlyTable` entities). Database-level privileges are recommended but not yet applied, so direct database access can still alter history.
- **Expiry:** nothing sets `Expired` automatically yet; the Agent job is outstanding.

See [database.md](../Architecture/database.md) and [api-surface.md](../Architecture/api-surface.md#key-states).
