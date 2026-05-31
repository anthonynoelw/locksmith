---
name: dotnet-security-audit
description: >
  Audit C# and ASP.NET Core code for security vulnerabilities. Trigger this skill
  whenever the user says "audit", "security review", "code review", "is this safe",
  "check for vulnerabilities", "review my auth", "pen test prep", or asks whether
  code handles input safely. Also trigger when reviewing controllers, services,
  repositories, middleware, deserialization logic, configuration files, or any code
  that handles user input, authentication, authorization, or external data.
  Prefer this skill over ad-hoc review for anything security-related.
---

# .NET Security Audit

Systematic, layer-by-layer security review of C# and ASP.NET Core code against
OWASP Top 10, CWE, and .NET-specific vulnerability patterns.

## How to run an audit

1. **Identify the audit scope** — single file, feature area, or full codebase.
2. **Work through every checklist in `references/owasp-dotnet.md`** relevant to that scope.
3. **For each finding** — record severity, location, evidence, and a concrete fix.
4. **Produce the structured report** using the output format below.

Read `references/owasp-dotnet.md` before beginning any audit — it contains the
full vulnerability catalogue with .NET-specific patterns and fixes.

---

## Quick triage checklist

Run this first to prioritize where to focus the deep audit.

### Injection (SQL, command, LDAP, XSS)
- [ ] Any raw string concatenation passed to `SqlCommand`, `ExecuteRawSql`, or `FromSqlRaw`?
- [ ] Any `Process.Start` or `cmd.exe` call that includes user-controlled input?
- [ ] Any `Response.Write`, `Html.Raw`, or `innerHTML` set from user input without encoding?
- [ ] Any LDAP filter built from user input without escaping?

### Secrets and sensitive data
- [ ] Connection strings, API keys, passwords, or tokens hardcoded in source files?
- [ ] Secrets committed in `appsettings.json` or `appsettings.Production.json`?
- [ ] Sensitive values logged via `ILogger`, `Console.WriteLine`, or `Debug.WriteLine`?
- [ ] PII or secrets returned in API responses or error messages?

### Authentication and authorization
- [ ] Every non-public endpoint has `[Authorize]` or equivalent policy enforcement?
- [ ] JWT validation configured with `ValidateIssuer`, `ValidateAudience`, `ValidateLifetime` all `true`?
- [ ] No `[AllowAnonymous]` on endpoints that expose sensitive operations?
- [ ] Role and policy checks use built-in ASP.NET Core mechanisms — not hand-rolled string comparisons?
- [ ] Password hashing uses `PasswordHasher<T>` or Argon2/bcrypt — not MD5/SHA1/plain text?

### Deserialization
- [ ] `JsonSerializer` (System.Text.Json) used — not `BinaryFormatter`, `NetDataContractSerializer`, or `JavaScriptSerializer`?
- [ ] No `TypeNameHandling.Auto` or `TypeNameHandling.All` in Newtonsoft.Json settings?
- [ ] XML deserialization using `XmlReader` with `DtdProcessing.Prohibit`?
- [ ] No deserialization of untrusted data into `object` or `dynamic`?

### Dependency and supply chain
- [ ] `dotnet list package --vulnerable` run recently with no high/critical results?
- [ ] No packages sourced from unverified feeds or without package lock files?

---

## Severity definitions

Use these consistently across all findings.

| Severity | Definition | Example |
|---|---|---|
| **Critical** | Exploitable with no authentication; direct data loss or RCE possible | SQL injection on a public endpoint |
| **High** | Exploitable by authenticated users; significant data exposure | IDOR allowing access to other users' records |
| **Medium** | Requires specific conditions; partial data exposure | Missing `SameSite` on auth cookies |
| **Low** | Defence-in-depth gap; no direct exploit path | Stack traces in error responses |
| **Info** | Best-practice deviation with minimal risk | Missing `X-Content-Type-Options` header |

---

## Output format

ALWAYS produce findings using this exact structure:

---

### Audit report: [scope / file name]

**Audited:** [date]
**Scope:** [what was reviewed]

#### Risk summary

| Severity | Count |
|---|---|
| Critical | N |
| High | N |
| Medium | N |
| Low | N |
| Info | N |

---

#### Finding [N]: [Short descriptive title]

**Severity:** Critical / High / Medium / Low / Info
**Category:** Injection / Secrets / Authorization / Deserialization / Other
**Location:** `FileName.cs`, line N (method name)

**Evidence**
```csharp
// The vulnerable code as it appears
```

**Why this is a problem**
One to three sentences explaining the attack vector and potential impact.

**Fix**
```csharp
// The corrected code
```

**Reference:** [CWE-XXX / OWASP ASVS X.Y.Z]

---

*(Repeat for each finding)*

---

#### Passed checks
List the checklist items that were verified clean — this gives the reviewer
confidence that areas were actually inspected, not just skipped.

---

## Vulnerability patterns to always flag

### SQL injection
```csharp
// CRITICAL — raw string interpolation in SQL
var sql = $"SELECT * FROM Orders WHERE CustomerId = '{customerId}'";
db.Database.ExecuteSqlRaw(sql);

// CRITICAL — string concatenation in FromSqlRaw
var orders = db.Orders.FromSqlRaw("SELECT * FROM Orders WHERE Id = " + id);

// SAFE — parameterised
var orders = db.Orders.FromSqlRaw("SELECT * FROM Orders WHERE Id = {0}", id);
// or better — use LINQ
var orders = db.Orders.Where(o => o.Id == id);
```

### Command injection
```csharp
// CRITICAL — user input in shell command
Process.Start("cmd.exe", $"/c ping {userInput}");

// SAFE — validate and whitelist; never pass raw input to shell
if (!IPAddress.TryParse(userInput, out _))
    return BadRequest("Invalid IP address.");
Process.Start("ping", userInput);
```

### Hardcoded secrets
```csharp
// HIGH — hardcoded connection string / API key
private const string ConnectionString = "Server=prod-db;Password=S3cr3t!";
private readonly string _apiKey = "sk-live-abc123xyz";

// SAFE — read from configuration backed by Key Vault / environment variables
private readonly string _apiKey = configuration["ExternalApi:Key"]!;
```

### Missing authorization
```csharp
// HIGH — no [Authorize] on a sensitive endpoint
[HttpDelete("{id}")]
public async Task<IActionResult> DeleteUser(Guid id) { }

// HIGH — IDOR: uses caller-supplied id without ownership check
public async Task<Order> GetOrderAsync(Guid orderId)
    => await _db.Orders.FindAsync(orderId); // any authenticated user can fetch any order

// SAFE — enforce ownership
public async Task<Order> GetOrderAsync(Guid orderId, ClaimsPrincipal caller)
{
    var callerId = Guid.Parse(caller.FindFirstValue(ClaimTypes.NameIdentifier)!);
    return await _db.Orders
        .Where(o => o.Id == orderId && o.CustomerId == callerId)
        .FirstOrDefaultAsync()
        ?? throw new ForbiddenException();
}
```

### Insecure deserialization
```csharp
// CRITICAL — BinaryFormatter (banned in .NET 7+, throws by default in .NET 9+)
var obj = (MyType)new BinaryFormatter().Deserialize(stream);

// CRITICAL — Newtonsoft TypeNameHandling allows type confusion attacks
var settings = new JsonSerializerSettings { TypeNameHandling = TypeNameHandling.All };
var obj = JsonConvert.DeserializeObject(untrustedJson, settings);

// SAFE — System.Text.Json with explicit type
var obj = JsonSerializer.Deserialize<MyDto>(untrustedJson, _options);

// SAFE — Newtonsoft with TypeNameHandling.None (the default)
var obj = JsonConvert.DeserializeObject<MyDto>(untrustedJson);
```

---

## Reference files

| File | When to read |
|---|---|
| `references/owasp-dotnet.md` | Before any audit — full vulnerability catalogue with .NET patterns |