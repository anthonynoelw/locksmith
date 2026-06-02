# ADR-002: API Key Creation

**Status:** Pending  
**Date:** 2025-5-31  
**Related concept:** Concept 002 — API Key Creation

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
This is because the the System can generate keys with higher entropy, follow specific rules and be more consistent. 
Also it improves the usability of the system by not requiring further user action. 
Although this comes with the tradeoff of less flexibility for the user and more complexity in the system and implementation.
This decision can be revisited if users require more flexibility in the future.


## Security implications

The system generated key option provides better security by ensuring that the keys are generated with higher entropy and follow specific rules. 
This reduces the risk of weak keys being used. 
However, it also means that the system needs to be more complex and secure to prevent the keys from being compromised. 

## Consequences

The need of more API endpoints to handle the key generation process will be reduced by using a system generated key. 
Although it is now dependent on the system to generate a secure key, which leads to a slight increase in complexity.
The most important consequence is that the system needs to be more secure to prevent the keys from being compromised.

## What this taught me

TODO: not implemented yet

Write 2–4 sentences in your own words. What did implementing this decision
teach you about how secure systems are designed? This is a learning journal,
not just a reference document — the reflection section is not optional.

