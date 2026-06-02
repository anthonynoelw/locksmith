# ADR-001: API Key Lifecycle

**Status:** Accepted  
**Date:** 2025-5-31   
**Related concept:** Concept 1 — API Key Lifecycle

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

The separate table approach was better because it allows for better auditability and traceability of the API key status changes than the one-table option. The status table holds all status changes for an API key and is append-only. Soft deletes fit naturally into this model — a `Deleted` status record signals that the key is no longer valid, while the key hash row and the full status history are preserved intact. This is important for security auditing and compliance reasons. I lose some simplicity in implementation, maintainability, and performance. I may consider the one-table approach if performance is not acceptable due to the additional data.

## Security implications

This separation of concerns allows us to track the change of access from an API key, which can be used to detect suspicious activity and prevent unauthorized access. The append-only nature of the status table ensures that the history of API key status changes cannot be tampered with, and soft deletes extend that guarantee to deletion events — the moment a key was retired is permanently on record. This allows an investigation to be conducted more easily and quickly. The soft delete approach also means that any system checking key validity simply queries the latest status entry; a key bearing a `Deleted` status is rejected without any physical row removal that could otherwise erase forensic evidence.

## Consequences

Telemetry of status changes, including deletions, can therefore be more easily implemented and maintained, but the maintenance of the status table adds some complexity to the implementation. Queries for "active" keys must filter on the latest status entry rather than on the absence of a row, which adds a small but consistent overhead to key-validation hot paths.

## What this taught me

TODO: not implemented yet

Write 2–4 sentences in your own words. What did implementing this decision
teach you about how secure systems are designed? This is a learning journal,
not just a reference document — the reflection section is not optional.

