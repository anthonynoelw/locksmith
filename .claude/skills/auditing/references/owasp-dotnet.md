# OWASP .NET Security Reference

Full vulnerability catalogue mapped to .NET 10 / ASP.NET Core patterns. Each section covers
the vulnerability, how it manifests in C#, detection signals, and concrete fixes.

---

## Table of contents

1. [A01 — Broken Access Control](#a01--broken-access-control)
2. [A02 — Cryptographic Failures](#a02--cryptographic-failures)
3. [A03 — Injection](#a03--injection)
4. [A04 — Insecure Design](#a04--insecure-design)
5. [A05 — Security Misconfiguration](#a05--security-misconfiguration)
6. [A06 — Vulnerable and Outdated Components](#a06--vulnerable-and-outdated-components)
7. [A07 — Identification and Authentication Failures](#a07--identification-and-authentication-failures)
8. [A08 — Software and Data Integrity Failures (Insecure Deserialization)](#a08--software-and-data-integrity-failures)
9. [A09 — Security Logging and Monitoring Failures](#a09--security-logging-and-monitoring-failures)
10. [A10 — Server-Side Request Forgery (SSRF)](#a10--server-side-request-forgery)
11. [Secrets and sensitive data](#secrets-and-sensitive-data)
12. [HTTP security headers](#http-security-headers)
13. [File upload security](#file-upload-security)
14. [Mass assignment](#mass-assignment)

---

## A01 — Broken Access Control

**CWE-284, CWE-285, CWE-639 (IDOR)**

### What it looks like in .NET

#### Missing [Authorize] attribute

```csharp
// VULNERABLE — endpoint is publicly accessible
[HttpDelete("{id}")]
public async Task<IActionResult> DeleteUser(Guid id)
{
    await _userService.DeleteAsync(id);
    return NoContent();
}

// SAFE — require authentication and an admin policy
[Authorize(Policy = "RequireAdmin")]
[HttpDelete("{id}")]
public async Task<IActionResult> DeleteUser(Guid id)
{
    await _userService.DeleteAsync(id);
    return NoContent();
}
```

#### Insecure Direct Object Reference (IDOR)

User-supplied IDs are used without verifying ownership. Any authenticated user
can access another user's resources.

```csharp
// VULNERABLE — orderId comes from the URL; no ownership check
public async Task<OrderDto> GetOrderAsync(Guid orderId)
    => await _db.Orders.FindAsync(orderId);

// SAFE — always scope queries to the authenticated caller's identity
public async Task<OrderDto> GetOrderAsync(Guid orderId, ClaimsPrincipal caller)
{
    var callerId = caller.GetUserId(); // extension method reading NameIdentifier claim
    var order = await _db.Orders
        .Where(o => o.Id == orderId && o.CustomerId == callerId)
        .FirstOrDefaultAsync()
        ?? throw new ForbiddenException("Order not found or access denied.");
    return order.ToDto();
}
```

#### Privilege escalation via claim manipulation

```csharp
// VULNERABLE — role read from a request header or body (attacker-controlled)
var role = Request.Headers["X-Role"];
if (role == "Admin") { /* grant access */ }

// SAFE — always read claims from the validated JWT / cookie principal
var isAdmin = User.IsInRole("Admin");
var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
```

#### Minimal API — global authorization policy

```csharp
// Program.cs — require auth everywhere by default, opt out explicitly
builder.Services.AddAuthorizationBuilder()
    .SetFallbackPolicy(new AuthorizationPolicyBuilder()
        .RequireAuthenticatedUser()
        .Build());

// Explicitly open endpoints still work
app.MapGet("/health", () => "ok").AllowAnonymous();
```

#### Audit signals

- `[AllowAnonymous]` on endpoints that perform mutations or return PII
- LINQ queries without a caller-identity `Where` clause on user-scoped data
- Authorization checks done in the service layer only, bypassed if caller hits repo directly
- `User.IsInRole("Admin")` replaced with custom string comparisons

---

## A02 — Cryptographic Failures

**CWE-327, CWE-328, CWE-916**

### Weak or broken hashing

```csharp
// CRITICAL — MD5/SHA1 are broken for passwords
var hash = MD5.HashData(Encoding.UTF8.GetBytes(password));
var hash = SHA1.HashData(Encoding.UTF8.GetBytes(password));

// HIGH — SHA-256 without salt is still vulnerable to rainbow tables
var hash = SHA256.HashData(Encoding.UTF8.GetBytes(password));

// SAFE — use ASP.NET Core's built-in password hasher (PBKDF2-SHA512 + salt)
var hasher = new PasswordHasher<User>();
var hashed = hasher.HashPassword(user, plainTextPassword);
var result = hasher.VerifyHashedPassword(user, hashed, plainTextPassword);
// result == PasswordVerificationResult.Success

// SAFE — or use BCrypt.Net / Argon2 via a well-maintained package
```

### Weak encryption

```csharp
// CRITICAL — DES/3DES are deprecated
using var des = DES.Create();

// CRITICAL — ECB mode reveals patterns in ciphertext
using var aes = Aes.Create();
aes.Mode = CipherMode.ECB; // never use ECB

// SAFE — AES-GCM (authenticated encryption, detects tampering)
using var aes = new AesGcm(key, AesGcm.TagByteSizes.MaxSize);
aes.Encrypt(nonce, plaintext, ciphertext, tag);
```

### Data Protection API (ASP.NET Core)

```csharp
// SAFE — use Data Protection for cookie / token encryption
builder.Services.AddDataProtection()
    .PersistKeysToAzureBlobStorage(/* blob URI */)
    .ProtectKeysWithAzureKeyVault(/* key URI */);

// Keys must be persisted in production — in-memory keys break after restart
// and make rolling deployments impossible
```

### TLS and transport

```csharp
// VULNERABLE — disabling TLS validation (common in dev, leaks into prod)
handler.ServerCertificateCustomValidationCallback = (_, _, _, _) => true;

// SAFE — never disable in production; use dev certs for local development only
// Enforce HTTPS everywhere:
app.UseHttpsRedirection();
app.UseHsts();
```

### Sensitive data in transit

```csharp
// Flag: sensitive data in query strings (logged by proxies, browser history)
GET /api/reset-password?token=abc123&email=user@example.com

// SAFE — pass sensitive values in the request body only, over HTTPS
POST /api/reset-password
{ "token": "abc123", "email": "user@example.com" }
```

### Audit signals

- `MD5`, `SHA1`, `DES`, `TripleDES` anywhere in the codebase
- `CipherMode.ECB`
- `ServerCertificateCustomValidationCallback` returning `true`
- Passwords stored or compared as plain text
- Sensitive tokens in URL parameters

---

## A03 — Injection

**CWE-89 (SQL), CWE-78 (Command), CWE-79 (XSS), CWE-90 (LDAP)**

### SQL injection

```csharp
// CRITICAL — string interpolation in raw SQL
db.Database.ExecuteSqlRaw($"SELECT * FROM Users WHERE Email = '{email}'");
db.Orders.FromSqlRaw("SELECT * FROM Orders WHERE Id = " + id);

// CRITICAL — SqlCommand with string concatenation
var cmd = new SqlCommand("SELECT * FROM Users WHERE Name = '" + name + "'", conn);

// SAFE — parameterised raw SQL
db.Database.ExecuteSqlRaw("SELECT * FROM Users WHERE Email = {0}", email);

// SAFE — EF Core LINQ (always parameterised)
db.Users.Where(u => u.Email == email).FirstOrDefaultAsync();

// SAFE — SqlCommand with parameters
var cmd = new SqlCommand("SELECT * FROM Users WHERE Name = @name", conn);
cmd.Parameters.AddWithValue("@name", name);
```

### Command injection

```csharp
// CRITICAL — user input in shell arguments
var output = Process.Start("bash", $"-c {userInput}");
Process.Start(new ProcessStartInfo("cmd.exe", $"/c {command}"));

// SAFE — validate input strictly; never use shell=true for user input
if (!Regex.IsMatch(filename, @"^[\w\-]+\.(pdf|csv)$"))
    return BadRequest("Invalid filename.");

var psi = new ProcessStartInfo("convert")
{
    ArgumentList = { filename, outputPath }, // ArgumentList prevents shell injection
    RedirectStandardOutput = true,
    UseShellExecute = false   // MUST be false when passing ArgumentList
};
Process.Start(psi);
```

### Cross-site scripting (XSS)

```csharp
// HIGH — unencoded user input rendered in Razor
@Html.Raw(userContent)       // bypasses Razor's automatic encoding
<div>@Html.Raw(Model.Bio)</div>

// SAFE — Razor auto-encodes by default; never use Html.Raw with user content
<div>@Model.Bio</div>

// SAFE — explicit HTML encoding when building strings outside Razor
var encoded = HtmlEncoder.Default.Encode(userInput);

// For rich text: use a sanitizer library (e.g., HtmlSanitizer on NuGet)
var sanitized = sanitizer.Sanitize(userHtml);
```

### LDAP injection

```csharp
// HIGH — user input in LDAP filter
var filter = $"(&(objectClass=user)(sAMAccountName={username}))";
var entry = directorySearcher.FindOne();

// SAFE — escape special LDAP characters
var safeUsername = username
    .Replace("\\", "\\5c")
    .Replace("*",  "\\2a")
    .Replace("(",  "\\28")
    .Replace(")",  "\\29")
    .Replace("\0", "\\00");
var filter = $"(&(objectClass=user)(sAMAccountName={safeUsername}))";
```

### Header injection

```csharp
// MEDIUM — user input written directly to a response header
Response.Headers["Location"] = userInput;  // can inject newlines → header splitting

// SAFE — validate and encode; for redirects use built-in helpers only
if (!Uri.TryCreate(returnUrl, UriKind.Relative, out _))
    return BadRequest();
return LocalRedirect(returnUrl); // LocalRedirect rejects absolute URLs
```

### Audit signals

- `FromSqlRaw`, `ExecuteSqlRaw`, `ExecuteSqlInterpolated` — check for non-parameterised use
- `SqlCommand` — check `CommandText` property for concatenation
- `Html.Raw(` — nearly always a finding
- `Process.Start` with `UseShellExecute = true` and user input
- `Response.Headers[` set from user-controlled values

---

## A04 — Insecure Design

**CWE-657, CWE-840**

### Missing rate limiting

```csharp
// Unauthenticated endpoints without rate limiting enable brute-force attacks
app.MapPost("/auth/login", LoginHandler); // no rate limit — brute-forceable

// SAFE — .NET 9+ built-in rate limiter
builder.Services.AddRateLimiter(o =>
{
    o.AddFixedWindowLimiter("login", opts =>
    {
        opts.PermitLimit = 5;
        opts.Window = TimeSpan.FromMinutes(1);
        opts.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
        opts.QueueLimit = 0;
    });
});

app.MapPost("/auth/login", LoginHandler)
   .RequireRateLimiting("login");
```

### Predictable resource IDs

```csharp
// MEDIUM — sequential integer IDs allow enumeration
GET /api/orders/1001
GET /api/orders/1002

// SAFE — use Guid or Sqids for public-facing IDs
public Guid Id { get; init; } = Guid.NewGuid();
```

### Unrestricted file operations

```csharp
// CRITICAL — path traversal
var filePath = Path.Combine(_uploadDir, userFilename);
// userFilename = "../../appsettings.json" → reads config

// SAFE — strip directory components, validate extension, resolve and verify
var sanitized = Path.GetFileName(userFilename); // strips any directory components
if (!AllowedExtensions.Contains(Path.GetExtension(sanitized).ToLowerInvariant()))
    return BadRequest("File type not allowed.");
var fullPath = Path.GetFullPath(Path.Combine(_uploadDir, sanitized));
if (!fullPath.StartsWith(_uploadDir, StringComparison.OrdinalIgnoreCase))
    return BadRequest("Invalid path.");
```

---

## A05 — Security Misconfiguration

**CWE-16**

### Detailed error responses in production

```csharp
// HIGH — full stack trace returned to client
app.UseDeveloperExceptionPage(); // must NEVER run in production

// SAFE — environment guard
if (app.Environment.IsDevelopment())
    app.UseDeveloperExceptionPage();
else
    app.UseExceptionHandler("/error");

// SAFE — generic problem details with no internal info
app.UseExceptionHandler(exceptionApp =>
{
    exceptionApp.Run(async ctx =>
    {
        ctx.Response.StatusCode = 500;
        await ctx.Response.WriteAsJsonAsync(new ProblemDetails
        {
            Title = "An unexpected error occurred.",
            Status = 500
        });
    });
});
```

### CORS misconfiguration

```csharp
// CRITICAL — allows any origin including attacker-controlled sites
builder.Services.AddCors(o => o.AddDefaultPolicy(p =>
    p.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader()));

// SAFE — restrict to known origins
builder.Services.AddCors(o => o.AddDefaultPolicy(p =>
    p.WithOrigins("https://app.example.com")
     .WithMethods("GET", "POST", "PUT", "DELETE")
     .WithHeaders("Authorization", "Content-Type")));
```

### Debug / Swagger in production

```csharp
// MEDIUM — Swagger UI exposed in production leaks API surface
app.UseSwagger();
app.UseSwaggerUI();

// SAFE — development only
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}
```

### Audit signals

- `UseDeveloperExceptionPage()` without an `IsDevelopment()` guard
- `AllowAnyOrigin()` — always a finding in production
- `UseSwagger` without environment guard
- `app.Urls` or Kestrel config listening on `0.0.0.0` without TLS in production

---

## A06 — Vulnerable and Outdated Components

**CWE-1104**

### Detection commands

```bash
# List all packages with known vulnerabilities
dotnet list package --vulnerable

# Include transitive dependencies
dotnet list package --vulnerable --include-transitive

# Check for outdated packages
dotnet list package --outdated
```

### Package lock files (reproducible builds)

```bash
# Enable package lock files to prevent supply chain substitution
dotnet nuget enable-packages-lock-file

# Enforce in CI — fails build if lock file is out of sync
dotnet restore --locked-mode
```

### Audit signals

- No `packages.lock.json` in the repository
- NuGet packages pinned to ranges (`>= 1.0.0`) rather than exact versions in sensitive projects
- Packages from non-Microsoft feeds without signature verification

---

## A07 — Identification and Authentication Failures

**CWE-287, CWE-306, CWE-798**

### JWT misconfiguration

```csharp
// CRITICAL — signature validation disabled
new TokenValidationParameters
{
    ValidateIssuerSigningKey = false,  // anyone can forge tokens
    ValidateIssuer = false,
    ValidateAudience = false,
    ValidateLifetime = false           // expired tokens accepted indefinitely
};

// SAFE — validate everything
new TokenValidationParameters
{
    ValidateIssuerSigningKey = true,
    IssuerSigningKey = new SymmetricSecurityKey(keyBytes),
    ValidateIssuer = true,
    ValidIssuer = config["Jwt:Issuer"],
    ValidateAudience = true,
    ValidAudience = config["Jwt:Audience"],
    ValidateLifetime = true,
    ClockSkew = TimeSpan.FromSeconds(30)
};
```

### Algorithm confusion attack

```csharp
// CRITICAL — allowing "none" algorithm or HS256 when RS256 is expected
// Always whitelist the exact algorithm
new TokenValidationParameters
{
    ValidAlgorithms = new[] { SecurityAlgorithms.RsaSha256 }
};
```

### Weak session / cookie configuration

```csharp
// MEDIUM — cookies without Secure, HttpOnly, SameSite
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(o =>
    {
        o.Cookie.HttpOnly = true;          // prevents JavaScript access
        o.Cookie.SecurePolicy = CookieSecurePolicy.Always; // HTTPS only
        o.Cookie.SameSite = SameSiteMode.Strict;           // CSRF protection
        o.ExpireTimeSpan = TimeSpan.FromHours(8);
        o.SlidingExpiration = false;       // fixed expiry
    });
```

### Brute-force on login

```csharp
// Enforce lockout via ASP.NET Core Identity
builder.Services.Configure<LockoutOptions>(o =>
{
    o.MaxFailedAccessAttempts = 5;
    o.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
    o.AllowedForNewUsers = true;
});
```

### Audit signals

- `ValidateIssuerSigningKey = false`, `ValidateLifetime = false`
- `RequireHttpsMetadata = false` outside of development
- Passwords stored as plain text or MD5/SHA1
- No account lockout configured
- `[AllowAnonymous]` on `ChangePassword` or `DeleteAccount`

---

## A08 — Software and Data Integrity Failures

**CWE-502 (Insecure Deserialization), CWE-345**

### Banned deserializers

```csharp
// CRITICAL — BinaryFormatter (banned; throws NotSupportedException in .NET 9+ by default)
var obj = (MyType)new BinaryFormatter().Deserialize(stream);

// CRITICAL — NetDataContractSerializer
var obj = (MyType)new NetDataContractSerializer().ReadObject(stream);

// CRITICAL — LosFormatter / ObjectStateFormatter
// All of these allow remote code execution via type confusion

// SAFE — System.Text.Json with explicit type binding
var dto = JsonSerializer.Deserialize<MyDto>(stream, _options);
```

### Newtonsoft.Json TypeNameHandling

```csharp
// CRITICAL — TypeNameHandling.All / Auto with untrusted input
var settings = new JsonSerializerSettings
{
    TypeNameHandling = TypeNameHandling.All  // attacker controls the $type field → RCE
};
var obj = JsonConvert.DeserializeObject(untrustedJson, settings);

// SAFE — TypeNameHandling.None (default) with explicit type
var dto = JsonConvert.DeserializeObject<MyDto>(untrustedJson);

// If you must use TypeNameHandling, add a SerializationBinder whitelist:
settings.SerializationBinder = new SafeSerializationBinder(new[]
{
    typeof(MyDto), typeof(OtherSafeType)
});
```

### XML deserialization

```csharp
// HIGH — XmlSerializer with DTD processing enabled (XXE attack)
var reader = XmlReader.Create(stream);  // DTD enabled by default in older .NET

// SAFE — disable DTD processing
var settings = new XmlReaderSettings
{
    DtdProcessing = DtdProcessing.Prohibit,
    XmlResolver = null
};
var reader = XmlReader.Create(stream, settings);
var obj = (MyType)new XmlSerializer(typeof(MyType)).Deserialize(reader);
```

### YAML deserialization (YamlDotNet)

```csharp
// HIGH — YamlDotNet deserializing to object can execute arbitrary types
var deserializer = new Deserializer();
var obj = deserializer.Deserialize<object>(yamlString); // dangerous

// SAFE — deserialize to a concrete known type only
var config = deserializer.Deserialize<AppConfig>(yamlString);
```

### Audit signals

- `BinaryFormatter`, `NetDataContractSerializer`, `LosFormatter`, `SoapFormatter`
- `TypeNameHandling` set to anything other than `None`
- `XmlReader.Create` without `DtdProcessing.Prohibit`
- `JsonConvert.DeserializeObject(json)` (untyped overload) on untrusted input
- Deserializing to `object` or `dynamic` from an external source

---

## A09 — Security Logging and Monitoring Failures

**CWE-778, CWE-117**

### Log injection

```csharp
// MEDIUM — user input written to logs unescaped allows log forgery
_logger.LogInformation($"User logged in: {username}");
// attacker sets username = "admin\nINFO: Admin granted full access"

// SAFE — structured logging; Serilog / .NET ILogger escape newlines automatically
_logger.LogInformation("User logged in: {Username}", username);
```

### Logging sensitive data

```csharp
// HIGH — passwords, tokens, or PII in logs
_logger.LogDebug("Login attempt: user={Email}, password={Password}", email, password);
_logger.LogInformation("JWT: {Token}", jwtToken);

// SAFE — never log passwords, tokens, credit card numbers, or SSNs
_logger.LogDebug("Login attempt for user: {Email}", email);
// Log the outcome, not the credential
```

### Insufficient audit trail

Every security-relevant event must be logged with: who, what, when, from where.

```csharp
// Required security events to log
_logger.LogWarning("Authentication failed for {Email} from {IpAddress}", email, ip);
_logger.LogWarning("Authorization denied: user {UserId} attempted {Action} on {Resource}",
    userId, action, resourceId);
_logger.LogInformation("Password changed for user {UserId}", userId);
_logger.LogCritical("Account locked after {Attempts} failed attempts: {UserId}",
    attempts, userId);
```

### Audit signals

- String interpolation in log calls (defeats structured logging, can mask injection)
- `LogDebug` or `LogTrace` statements containing `password`, `token`, `secret`, `key`, `ssn`, `creditcard`
- No logging on failed authentication or authorization events
- No correlation ID / trace ID attached to log entries

---

## A10 — Server-Side Request Forgery

**CWE-918**

### What it looks like

Attacker supplies a URL that the server fetches — targeting internal services,
cloud metadata endpoints, or other hosts the server has access to.

```csharp
// CRITICAL — fetching an attacker-controlled URL with no validation
public async Task<string> FetchContentAsync(string url)
{
    return await _httpClient.GetStringAsync(url);
    // attacker passes: http://169.254.169.254/latest/meta-data/ (AWS metadata)
    // or: http://internal-db:5432/ (internal network probe)
}
```

### Safe URL fetching

```csharp
// SAFE — allowlist of permitted hosts
private static readonly HashSet<string> AllowedHosts = new(StringComparer.OrdinalIgnoreCase)
{
    "api.partner.com",
    "cdn.example.com"
};

public async Task<string> FetchContentAsync(string rawUrl)
{
    if (!Uri.TryCreate(rawUrl, UriKind.Absolute, out var uri))
        throw new ArgumentException("Invalid URL.");

    if (uri.Scheme != Uri.UriSchemeHttps)
        throw new SecurityException("Only HTTPS URLs are permitted.");

    if (!AllowedHosts.Contains(uri.Host))
        throw new SecurityException($"Host '{uri.Host}' is not in the allowlist.");

    return await _httpClient.GetStringAsync(uri);
}
```

### Audit signals

- `HttpClient.GetAsync(userInput)` without URL validation
- Any method that accepts a URL parameter and fetches it server-side
- Webhook registration endpoints that don't validate the target URL

---

## Secrets and sensitive data

### Detection patterns — search the codebase for these

```text
Regex patterns to grep for:
  password\s*=\s*["'][^"']+["']
  connectionstring\s*=\s*["'][^"']+["']
  apikey\s*=\s*["'][^"']+["']
  secret\s*=\s*["'][^"']+["']
  private\s+const\s+string.*[Kk]ey\s*=
  Bearer\s+[A-Za-z0-9\-_]+\.[A-Za-z0-9\-_]+\.[A-Za-z0-9\-_]+

Files to always check:
  appsettings.json
  appsettings.Production.json
  appsettings.Staging.json
  launchSettings.json
  docker-compose.yml
  .env  (should never be committed)
  *.csproj  (can contain inline secrets)
  Dockerfile
```

### Safe secrets management

```csharp
// Program.cs — layered configuration (rightmost wins)
builder.Configuration
    .AddJsonFile("appsettings.json", optional: false)
    .AddJsonFile($"appsettings.{env}.json", optional: true)
    .AddEnvironmentVariables()                    // overrides appsettings
    .AddAzureKeyVault(new Uri(kvUri), credential) // overrides env vars
    .AddUserSecrets<Program>(optional: true);     // local dev only

// Access secrets — never hardcode; always read from IConfiguration
var connectionString = builder.Configuration.GetConnectionString("Default")
    ?? throw new InvalidOperationException("Connection string 'Default' not found.");
```

### Masking secrets in logs (Serilog)

```csharp
Log.Logger = new LoggerConfiguration()
    .Destructure.ByTransforming<CreateUserRequest>(r => new
    {
        r.Email,
        Password = "***"  // mask before it reaches any sink
    })
    .WriteTo.Console()
    .CreateLogger();
```

---

## HTTP security headers

Every ASP.NET Core application in production must set these headers.

```csharp
// Recommended middleware (add before UseRouting)
app.Use(async (context, next) =>
{
    context.Response.Headers["X-Content-Type-Options"] = "nosniff";
    context.Response.Headers["X-Frame-Options"] = "DENY";
    context.Response.Headers["X-XSS-Protection"] = "0"; // modern browsers use CSP instead
    context.Response.Headers["Referrer-Policy"] = "strict-origin-when-cross-origin";
    context.Response.Headers["Permissions-Policy"] = "geolocation=(), microphone=()";
    context.Response.Headers["Content-Security-Policy"] =
        "default-Src 'self'; script-Src 'self'; style-Src 'self'; img-Src 'self' data:";
    await next();
});

// HSTS (UseHsts adds Strict-Transport-Security automatically)
builder.Services.AddHsts(o =>
{
    o.MaxAge = TimeSpan.FromDays(365);
    o.IncludeSubDomains = true;
    o.Preload = true;
});
```

Use [securityheaders.com](https://securityheaders.com) to verify production headers.

---

## File upload security

```csharp
public async Task<IActionResult> UploadAsync(IFormFile file, CancellationToken ct)
{
    // 1. Enforce file size limit
    if (file.Length > 10 * 1024 * 1024) // 10 MB
        return BadRequest("File too large.");

    // 2. Validate extension against an allowlist
    var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
    if (!new[] { ".pdf", ".png", ".jpg", ".csv" }.Contains(extension))
        return BadRequest("File type not permitted.");

    // 3. Validate MIME type (don't trust Content-Type header alone)
    if (!AllowedMimeTypes.Contains(file.ContentType))
        return BadRequest("Content type not permitted.");

    // 4. Sanitize the filename — never use the original name from the client
    var storedName = $"{Guid.NewGuid()}{extension}";

    // 5. Store outside the web root — never in wwwroot
    var storagePath = Path.Combine(_storageRoot, storedName);

    // 6. Verify resolved path is inside the storage root (path traversal defense)
    var resolved = Path.GetFullPath(storagePath);
    if (!resolved.StartsWith(_storageRoot, StringComparison.OrdinalIgnoreCase))
        return BadRequest("Invalid path.");

    await using var stream = System.IO.File.Create(resolved);
    await file.CopyToAsync(stream, ct);

    return Ok(new { filename = storedName });
}
```

---

## Mass assignment

Binding a model directly from user input may allow attackers to set fields they
should not control (e.g., `IsAdmin`, `Role`, `Balance`).

```csharp
// VULNERABLE — attacker can POST {"username":"alice","isAdmin":true}
[HttpPost]
public async Task<IActionResult> CreateUser([FromBody] User user)
{
    await _db.Users.AddAsync(user); // IsAdmin comes from attacker
    await _db.SaveChangesAsync();
    return Ok();
}

// SAFE — use a dedicated DTO; map explicitly
[HttpPost]
public async Task<IActionResult> CreateUser([FromBody] CreateUserRequest request)
{
    var user = new User
    {
        Username = request.Username,
        Email    = request.Email,
        IsAdmin  = false           // never from request
    };
    await _db.Users.AddAsync(user);
    await _db.SaveChangesAsync();
    return Ok();
}
```

### Audit signals

- Controller actions binding directly to entity classes (`[FromBody] User user`, `[FromBody] Order order`)
- `[Bind(Include="...")]` without verifying the list is complete and correct
- AutoMapper mapping from DTOs to entities with no property exclusions for sensitive fields
