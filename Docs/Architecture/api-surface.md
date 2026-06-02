# Locksmith — API Surface Design


## Example Endpoints

### POST /api/keys
Issues a new API key.

**Request headers:** `Idempotency-Key: <uuid>`  
**Request body:** `{ "ownerId": "string", "scopes": ["string"], "expiresInDays": int }`  
**Response 201:** `{ "keyId": "guid", "key": "string", "expiresAt": "datetime" }`  
**Response 409:** Idempotency key already used (returns original 201 body)

> The raw key appears in the response exactly once. It is never stored and
> cannot be retrieved again.

### GET /api/keys/{id}
Returns metadata for a key. Does not return the key value.

**Response 200:** `{ "keyId": "guid", "status": "Active|Revoked|Expired", ... }`  
**Response 401:** Key not found or not authenticated (identical body for both)

[... continue for all endpoints ...]

## Happy-path flow

1. Caller issues a key via POST /api/keys → receives `key` value once
2. Caller authenticates subsequent requests via X-API-Key header
3. Middleware validates key on every request
4. Key is rotated via POST /api/keys/{id}/rotate → old key immediately invalid
5. Key is revoked via DELETE /api/keys/{id}
