# Concept 002 — API Key Creation

**Completed:** 2025-05-31
**Related ADRs:** ADR-002 

## The questions I had to answer first

How can an API key be created securely in a way that the user can also retrieve it?

## My initial answer (before implementing)

I thought that an API key should be created via a secret key, salt and a secure hash algorithm.


### How a key is created

```mermaid
flowchart TD
    A([User]) -->|POST /api-keys| B[API receives request]
    B --> C{Is request valid?}
    C -->|No| D[Return 422 Unprocessable Entity]
    C -->|Yes| E[Generate cryptographically\nrandom secret key]
    E --> F[Generate random salt]
    F --> G[Hash secret + salt\nusing secure algorithm]
    G --> H[Persist hash + salt\n+ metadata to DB]
    H --> I[Return raw key\nto user — shown once only]
    I --> J([User stores key\nin their application])
    J --> K{User activates key\nvia API}
    K -->|No| L[Key remains inactive\n— requests rejected]
    K -->|Yes| M[Update key status\nto Active in DB]
    M --> N([Key is ready\nto authenticate requests])

    style D fill:#f66,color:#fff
    style I fill:#4c9,color:#fff
    style L fill:#f66,color:#fff
    style N fill:#4c9,color:#fff
```

> **Note:** The raw secret key is returned exactly once at creation time and never stored. All subsequent validation compares a re-hash of the provided key against the stored hash.


## Data 

```mermaid
classDiagram
    class ApiKeys {
        +Guid IdempotencyKey
        +string ApiKeyHash
        +string Salt
        +DateTime CreatedAt
        +DateTime UpdatedAt
        +string CreatedBy
        +string UpdatedBy
    }

    class ApiKeyState {
        +Guid IdempotencyKey
        +string ApiKeyHash
        +string State
        +DateTime CreatedAt
        +DateTime UpdatedAt
        +DateTime DeletedAt
        +string CreatedBy
        +string UpdatedBy
    }

    ApiKeys "1" --> "0..*" ApiKeyState : has states
```

## What the implementation revealed

[What did you discover while actually writing the code? Did anything surprise
you? Did your initial answer hold up?]

## The security principle this concept illustrates

[State it in one or two sentences, in plain language. Not a quote from a
textbook — your own words.]

## What I would do differently

[Is there anything in your implementation you're not fully satisfied with?
What would you improve if you revisited this?]
