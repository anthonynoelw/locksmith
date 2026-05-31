---
name: dotnet-pr-review
description: >
  Review a pull request for a .NET 10 / C# project. Trigger this skill whenever
  the user says "review this PR", "review my changes", "look at this diff",
  "give feedback on my code", "check my PR", "code review", or pastes a diff,
  file list, or branch description and asks for feedback. Runs four sequential
  review passes: conventions compliance, security audit, test coverage check,
  and a final verdict with a prioritised finding list.
---

# PR Review

A structured, four-pass review of every pull request against the project's
conventions, security standards, and testing requirements.

## How to run a PR review

Work through all four passes in order. Never skip a pass, even if the diff looks small.
A change that is two lines of code can introduce a security vulnerability or bypass a
test that would have caught a regression.

Read the relevant reference file at the start of each pass — they contain the
detailed checklists and scoring rules.

---

## Pass 1 — Conventions compliance

**Reference:** `references/conventions-review.md`
**Also read:** `../code-writing/references/conventions.md`

Check every changed file against the project's C# conventions:

- Naming: PascalCase types, `_camelCase` private fields, `Async` suffix, `I` prefix on interfaces
- File structure: file-scoped namespaces, one public type per file, `using` directive order
- Type choices: `sealed` on concrete classes, `record` for DTOs, `required`/`init` where appropriate
- Members: explicit access modifiers everywhere, guard clauses at method entry, `CancellationToken` threaded through
- Async: no `.Result` / `.Wait()`, no `async void`, no `new HttpClient()`
- Null safety: nullable enabled, `ArgumentNullException.ThrowIfNull`, no silent null suppression (`!`)
- Logging: structured log templates, no interpolation in log calls, no secrets in logs
- Anti-patterns table: `dynamic`, `Thread.Sleep`, swallowed exceptions, hardcoded secrets

For each violation, record: **file**, **line**, **rule broken**, **corrected code**.

---

## Pass 2 — Security audit

**Reference:** `references/security-review.md`
**Also read:** `../auditing/references/owasp-dotnet.md`

Scan every changed file for the OWASP Top 10 and .NET-specific vulnerabilities:

- **Injection** — raw SQL, command injection, `Html.Raw`, LDAP filter construction
- **Broken access control** — missing `[Authorize]`, IDOR (no ownership check on queries), privilege via header/body claims
- **Cryptographic failures** — MD5/SHA1/DES, ECB mode, TLS bypass, plain-text passwords
- **Insecure deserialization** — `BinaryFormatter`, `TypeNameHandling.All/Auto`, untyped `Deserialize`
- **Secrets** — hardcoded connection strings, API keys, passwords, tokens
- **Security misconfiguration** — `UseDeveloperExceptionPage` without env guard, `AllowAnyOrigin`, Swagger in prod
- **Mass assignment** — entity bound directly from `[FromBody]`, no DTO mapping
- **SSRF** — `HttpClient` called with user-supplied URL without allowlist

For each finding, record: **severity** (Critical / High / Medium / Low / Info), **file**, **line**, **attack vector**, **fix**.

---

## Pass 3 — Test coverage

**Reference:** `references/test-coverage-review.md`
**Also read:** `../testing/SKILL.md`

Verify that every meaningful change is covered by tests:

### Coverage requirements by change type

| Change type | Required test type | Minimum |
|---|---|---|
| New service method | Unit test (Moq) | Happy path + at least one error path |
| New repository method | Integration test (real DB) | Happy path + edge case |
| New API endpoint | Application test (HTTP) | 2xx happy path + auth failure (401/403) + validation failure (400/422) |
| Domain rule / invariant | Unit test | Every branch of the rule |
| Bug fix | Regression test that fails before the fix | One test that reproduces the bug |
| Schema migration | Integration test verifying the schema | Up() produces expected columns/indexes; Down() reverts cleanly |
| Security fix | Unit or application test | Verifies the vulnerability is closed |

### Test quality checks

- AAA structure present in every test (labelled `// Arrange`, `// Act`, `// Assert`)
- Naming convention: `MethodName_StateUnderTest_ExpectedBehavior`
- One behavior per test — no multi-assert omnibus tests
- FluentAssertions used — no bare `Assert.Equal` or `Assert.True`
- Mocks verify interactions, not just return values, where the call itself is the behavior
- No `Thread.Sleep` or `Task.Delay` in tests — use `FakeTimeProvider` or mock the dependency
- No magic strings/numbers in test data — use named constants or builders

### Running tests (record results in the report)

```bash
# Run all tests and collect results
dotnet test --logger "trx;LogFileName=results.trx" --results-directory ./test-results

# Run with coverage
dotnet test --collect:"XPlat Code Coverage" --results-directory ./test-results

# Show failed tests only
dotnet test --verbosity minimal | grep -E "FAIL|Error|Exception"

# Run a specific project
dotnet test tests/MyApp.UnitTests
dotnet test tests/MyApp.IntegrationTests
dotnet test tests/MyApp.ApplicationTests
```

For each missing test or quality violation, record: **what is untested**, **which test type is needed**, **example test name**.

---

## Pass 4 — Final verdict

**Reference:** `references/verdict-rubric.md`

Aggregate all findings from Passes 1–3 into the structured report below.
Assign an overall verdict and list all action items in priority order.

---

## Output format

ALWAYS produce the full review using this exact structure.

---

### PR Review: [PR title or branch name]

**Reviewed:** [date]
**Files changed:** N
**Passes run:** Conventions · Security · Test Coverage

---

#### Overall verdict

| Verdict | Meaning |
|---|---|
| ✅ **Approve** | No blockers. Minor suggestions noted below. |
| ⚠️ **Approve with comments** | No security or test blockers. Convention issues that must be fixed before merge. |
| 🔁 **Request changes** | One or more must-fix items. Cannot merge until resolved. |
| 🚫 **Block** | Critical security finding or zero test coverage on new logic. Needs redesign. |

**Verdict:** [one of the above]
**Reason:** [one sentence explaining the verdict]

---

#### Finding summary

| Pass | Severity | Count |
|---|---|---|
| Conventions | Must Fix / Should Fix / Suggestion | N / N / N |
| Security | Critical / High / Medium / Low / Info | N / N / N / N / N |
| Test Coverage | Missing / Quality | N / N |

---

#### Pass 1 — Conventions findings

For each finding:

**[C-N] [Rule name]**
**Severity:** Must Fix / Should Fix / Suggestion
**Location:** `FileName.cs` line N
**Issue:** What violates the convention and why it matters.
```csharp
// before
// after
```

*(List all findings, or write "✅ No convention violations found." if clean)*

---

#### Pass 2 — Security findings

For each finding:

**[S-N] [Short title]**
**Severity:** Critical / High / Medium / Low / Info
**Location:** `FileName.cs` line N (method name)
**Attack vector:** How an attacker exploits this.
**Fix:**
```csharp
// before
// after
```
**Reference:** CWE-XXX

*(List all findings, or write "✅ No security issues found." if clean)*

---

#### Pass 3 — Test coverage findings

For each missing or poor-quality test:

**[T-N] [What is missing]**
**Type:** Missing test / Quality issue
**Gap:** What scenario or behavior is not covered.
**Required test name:** `MethodName_StateUnderTest_ExpectedBehavior`
**Test type needed:** Unit / Integration / Application

*(List all findings, or write "✅ Test coverage is adequate." if clean)*

---

#### Test run results

```
Passed:  N
Failed:  N
Skipped: N

[List any failed tests with their error messages]
```

*(Write "⚠️ Tests were not run — run `dotnet test` and paste results to complete this review." if results were not provided)*

---

#### Action items (priority order)

Must fix before merge:
- [ ] [S-1] [Short description] — `FileName.cs:N`
- [ ] [T-1] [Short description]

Should fix before merge:
- [ ] [C-2] [Short description] — `FileName.cs:N`

Suggestions (non-blocking):
- [ ] [C-5] [Short description]

---

## Reference files

| File | Pass |
|---|---|
| `references/conventions-review.md` | Pass 1 — detailed convention checklist |
| `references/security-review.md` | Pass 2 — security scan checklist |
| `references/test-coverage-review.md` | Pass 3 — coverage requirements and quality checks |
| `references/verdict-rubric.md` | Pass 4 — scoring and verdict rules |