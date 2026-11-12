# ADR-002: API Key Creation

**Status:** Accepted  
**Date:** 2026-05-31  
**Related concept:** [Concept 002 — API Key Creation](../Concepts/concept-002-api-key-creation.md)

---

## Context

I needed to think about how to create a system where the user can create an API key securely and also retrieve it. 
The system needs to balance security with usability. 

## Options considered

| Option | What it is | Why it was considered |
|---|---|---|
| User generated key | The user is given the option enter a custom key with which the api key will be generated | This option gives the user more flexibility over the key generation process |
| System generated key | The system generates a random key with which the api key will be generated, after the generation process the key needs to be issued safely to the user | This option allows for better security and easier implementation |

## Decision

I decided to go with the system generated key option. 

## Reasoning

The system generated key option was chosen because it provides better security. 
This is because the system can generate keys with higher entropy, follow specific rules and be more consistent. 
Also it improves the usability of the system by not requiring further user action. 
Although this comes with the tradeoff of less flexibility for the user and more complexity in the system and implementation.
This decision can be revisited if users require more flexibility in the future.


## Security implications

The system generated key option provides better security by ensuring that the keys are generated with higher entropy and follow specific rules. 
This reduces the risk of weak keys being used. 
However, it also means that the system needs to be more complex and secure to prevent the keys from being compromised. 

## Consequences

Using a system generated key reduces the number of API endpoints needed, since there is no separate step for the user to supply or negotiate key material. 
In return, the system is now responsible for generating secure keys, which leads to a slight increase in complexity.
The most important consequence is that the system needs to be more secure to prevent the keys from being compromised.

## Implementation notes (v1.1.1)

How the decision was built (`CryptoService`, `CreateApiKeyService`):

- **Generated values:** a 32-byte random secret (base64url, prefixed `lk_`) and a 96-byte random idempotency key (base64url), both from `RandomNumberGenerator`. Both are returned to the caller once, at creation or rotation.
- **Lookup:** the secret and the idempotency key are each stored only as an unsalted SHA-256 hash (`SecretHash`, `IdempotencyKeyHash`), for unique-index lookups.
- **Safe re-issue ("retrieve it"):** the secret is stored encrypted with AES-256-GCM. The encryption key is derived per key with Argon2id from the plaintext idempotency key plus a random 32-byte salt stored on the `IdempotencyKeys` row. The caller recovers the secret via `POST /api-key/secret` with the idempotency key; Locksmith never stores the secret or the idempotency key in plaintext.
- **Trade-off:** the idempotency key is key material — losing it makes the secret unrecoverable, and anyone holding it (plus the bearer token) can retrieve the secret.

The encryption-at-rest design was an implementation choice made under this ADR rather than a separately recorded decision; see [overview.md](../Architecture/overview.md#cryptography-and-key-management) for details.
