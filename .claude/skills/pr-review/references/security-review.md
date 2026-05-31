# Security Review Checklist

Detailed checklist for Pass 2 of the PR review. The full vulnerability catalogue
with code examples lives in `../auditing/references/owasp-dotnet.md` — open it
for any finding that needs a detailed fix example.

Severity definitions:
- **Critical** — exploitable with no authentication; direct data loss or RCE
- **High** — exploitable by authenticated users; significant data exposure
- **Medium** — requires specific conditions; partial exposure or security degradation
- **Low** — defence-in-depth gap; no direct exploit path today
- **Info** — best-practice deviation worth noting

---

## Section 1 — Injection (A03)

Scan every changed file for user-controlled data flowing into dangerous sinks.

### SQL injection

- [ ] No raw string concatenation or interpolation in `ExecuteSqlRaw`, `FromSqlRaw`, `FromSqlInterpolated`

```csharp
// CRITICAL — flag immediately
db.Orders.FromSqlRaw("SELECT * FROM Orders WHERE Id = " + id);
db.Database.ExecuteSqlRaw($"DELETE FROM Sessions WHERE Token = '{token}'");

// SAFE — parameterised or LINQ
db.Orders.Where(o => o.Id == id);
db.Database.ExecuteSqlRaw("SELECT * FROM Orders WHERE Id = {0}", id);
```

- [ ] No `SqlCommand` with string-concatenated `CommandText`
- [ ] No Dapper queries built with string interpolation: `connection.QueryAsync<T>($"... {userInput}")`

### Command injection

- [ ] No `Process.Start` or `ProcessStartInfo` where `Arguments` or `FileName` contains user input
- [ ] If `Process.Start` is present, `UseShellExecute = false` and `ArgumentList` (not `Arguments`) is used

### XSS

- [ ] No `Html.Raw(` in Razor views with user-controlled content
- [ ] No `Response.Write(` with user content
- [ ] No Blazor `MarkupString` wrapping unsanitized user content

### LDAP / header injection

- [ ] No LDAP filter string built from user input without escaping
- [ ] No `Response.Headers[x] = userInput` without validation (header injection / CRLF)

---

## Section 2 — Broken access control (A01)

### Authorization on endpoints

- [ ] Every new or modified controller action / Minimal API endpoint has `[Authorize]` or an explicit authorization policy — or a documented reason for `[AllowAnonymous]`
- [ ] `[AllowAnonymous]` is not present on any endpoint that:
  - Returns user-specific data
  - Performs mutations (POST/PUT/PATCH/DELETE)
  - Exposes admin functionality

### IDOR — ownership checks

- [ ] Every query that returns user-scoped data includes a caller-identity `Where` clause

```csharp
// HIGH — any authenticated user can read any order
await db.Orders.FindAsync(orderId);

// SAFE — scoped to the caller's identity
await db.Orders
    .Where(o => o.Id == orderId && o.CustomerId == callerId)
    .FirstOrDefaultAsync(ct);
```

- [ ] The caller's identity is always read from `HttpContext.User` claims — never from request headers, query strings, or body parameters

### Privilege escalation

- [ ] Role/permission checks use `User.IsInRole()` or `IAuthorizationService` — not manual string comparisons against request data
- [ ] No role or permission value accepted from `Request.Headers`, `Request.Query`, or `[FromBody]`

---

## Section 3 — Cryptographic failures (A02)

- [ ] No use of `MD5`, `SHA1`, `DES`, `TripleDES` (broken algorithms)
- [ ] No `CipherMode.ECB` — use AES-GCM or AES-CBC with a random IV
- [ ] Password hashing uses `PasswordHasher<T>` (PBKDF2) or Argon2/bcrypt — not plain SHA-256
- [ ] No `ServerCertificateCustomValidationCallback` returning `true` unconditionally
- [ ] No `RequireHttpsMetadata = false` outside of a development environment guard
- [ ] Sensitive values (tokens, reset codes) not placed in URL query parameters

---

## Section 4 — Insecure deserialization (A08)

- [ ] No `BinaryFormatter` (banned; throws in .NET 9+ by default)
- [ ] No `NetDataContractSerializer`, `LosFormatter`, `SoapFormatter`
- [ ] No `JsonSerializerSettings { TypeNameHandling = TypeNameHandling.All }` or `TypeNameHandling.Auto` applied to untrusted input
- [ ] No `JsonConvert.DeserializeObject(json)` (untyped overload) on input from external sources
- [ ] `XmlReader` created with `DtdProcessing = DtdProcessing.Prohibit` — not the default settings
- [ ] No deserialization of untrusted data into `object` or `dynamic`

---

## Section 5 — Secrets and sensitive data

Run a quick grep over the diff for these patterns:

```bash
# Common secret patterns to check manually in the diff
grep -iE "(password|apikey|api_key|secret|connectionstring|token|bearer)\s*=\s*[\"'][^\"']{4,}" <files>
grep -iE "private\s+(readonly\s+)?const\s+string.*(key|secret|password)" <files>
```

- [ ] No connection strings in source files or `appsettings.json` committed to the repo
- [ ] No API keys, tokens, or passwords as `const` or field initializers
- [ ] No secrets in `launchSettings.json` (this file is often accidentally committed)
- [ ] `IConfiguration` / Key Vault / environment variables used for all secrets
- [ ] Sensitive values not returned in API response bodies (password hashes, internal IDs, etc.)
- [ ] No sensitive values in log messages (check all new `_logger.Log*` calls in the diff)

---

## Section 6 — Security misconfiguration (A05)

- [ ] `app.UseDeveloperExceptionPage()` is inside an `if (app.Environment.IsDevelopment())` guard
- [ ] `app.UseSwagger()` / `app.UseSwaggerUI()` is inside an `if (app.Environment.IsDevelopment())` guard
- [ ] CORS policy does not use `AllowAnyOrigin()` — origins are explicitly listed
- [ ] New middleware does not suppress or short-circuit authentication/authorization
- [ ] No new `[AllowAnonymous]` on previously protected endpoints

---

## Section 7 — Mass assignment

- [ ] Controller actions do **not** bind directly to entity classes

```csharp
// HIGH — attacker controls IsAdmin, Role, Balance, etc.
public async Task<IActionResult> Update([FromBody] User user) { }

// SAFE — explicit DTO with only the fields the caller is allowed to set
public async Task<IActionResult> Update([FromBody] UpdateUserRequest request) { }
```

- [ ] Every new `[FromBody]` parameter is a dedicated DTO, not a domain entity or EF model
- [ ] AutoMapper profiles (if used) do not map sensitive entity fields from DTOs without explicit `Ignore()`

---

## Section 8 — SSRF (A10)

- [ ] No `HttpClient.GetAsync(userInput)` or any `HttpClient` method called with a user-supplied URL without validation
- [ ] If a URL is accepted from the user, it is validated against a strict allowlist of permitted hosts
- [ ] Webhook registration endpoints validate the target URL scheme (HTTPS only) and host

---

## Section 9 — New dependencies

For every new NuGet package added in this PR:

- [ ] Package is from a reputable publisher (Microsoft, well-known OSS, your org)
- [ ] Package version is pinned (not a floating range like `>= 1.0`)
- [ ] `dotnet list package --vulnerable` shows no high/critical advisories for the new package

```bash
dotnet list package --vulnerable --include-transitive
```

---

## Security finding quick-reference

| Pattern in diff | Severity | Rule |
|---|---|---|
| `FromSqlRaw(` + string concat/interpolation | Critical | SQL injection |
| `Html.Raw(` with user data | High | XSS |
| Missing `[Authorize]` on mutating endpoint | High | Broken access control |
| `FindAsync(id)` with no ownership check | High | IDOR |
| `MD5` / `SHA1` / `DES` | High | Weak cryptography |
| `TypeNameHandling.All` or `.Auto` | Critical | Insecure deserialization |
| `BinaryFormatter` | Critical | Insecure deserialization (banned) |
| Hardcoded password / API key | High | Secret exposure |
| `AllowAnyOrigin()` | Medium | CORS misconfiguration |
| `UseDeveloperExceptionPage` without env guard | Medium | Info disclosure |
| `new HttpClient(url_from_user)` | High | SSRF |
| `[FromBody] EntityClass` | High | Mass assignment |
| `Response.Headers[x] = userInput` | Medium | Header injection |
