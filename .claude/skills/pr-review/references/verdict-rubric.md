# Verdict Rubric

Rules for assigning the final verdict and prioritising action items in Pass 4.
Apply these rules mechanically — do not let subjective impressions override them.

---

## Verdict rules (apply in order — first match wins)

### 🚫 Block

Assign **Block** if ANY of the following is true:

**Security:**
- Any **Critical** security finding (SQL injection, insecure deserialization, RCE vector, etc.)
- Any **High** security finding on an unauthenticated endpoint
- Hardcoded credentials or secrets in source code

**Tests:**
- New public API surface (service methods, endpoints) with **zero** tests
- A bug fix with no regression test that would have caught the original bug
- **Test failures** — any test in the suite currently failing (unless pre-existing and tracked)

**Conventions:**
- Code that **will not compile** due to a convention violation (e.g. missing `override`, wrong base type)

---

### 🔁 Request changes

Assign **Request changes** if ANY of the following is true and **Block** does not apply:

**Security:**
- Any **High** security finding on an authenticated endpoint
- Any **Medium** security finding that is straightforward to exploit

**Tests:**
- Missing tests for at least one complete feature area (e.g. new endpoint has a 200 test but no 401/403 test)
- Test quality issue that would allow a real bug to pass undetected (e.g. assertion always passes, mock not verified)
- New `[Skip]` attribute without a linked issue and expiry date

**Conventions:**
- **Must Fix** convention violations: anti-patterns (`dynamic`, `.Result`, `async void`, `Html.Raw`), missing guard clauses on public methods, `.Result`/`.Wait()` in production code
- Private field naming violations (affects searchability and team consistency)
- Missing `sealed` on a concrete service/repository class added in this PR

---

### ⚠️ Approve with comments

Assign **Approve with comments** if ALL of the following are true:
- No Critical or High security findings
- No missing test coverage for new public surface
- No Must Fix convention violations
- At least one **Should Fix** or **Medium** finding exists that the author should address before merge

Examples:
- Missing XML doc comments on new public types
- Inconsistent naming (`_camelCase` field named without underscore on one of five new fields)
- Medium security finding (e.g. missing `SameSite` on a non-auth cookie)
- Test exists but uses bare `Assert.Equal` instead of FluentAssertions
- Log call uses string interpolation instead of a structured template

---

### ✅ Approve

Assign **Approve** if ALL of the following are true:
- No security findings of Medium or above
- Full test coverage for all new public surface
- No Must Fix or Should Fix convention violations
- Only Info / Suggestion findings remain (or none at all)

---

## Finding priority ordering

Within the action items list, order findings as follows:

```
Priority 1 — Must fix before merge (any of):
  - Critical security findings
  - High security findings
  - Missing tests for new public surface
  - Test failures
  - Must Fix convention violations (anti-patterns, compile issues)

Priority 2 — Should fix before merge (any of):
  - Medium security findings
  - Missing edge-case test coverage
  - Should Fix convention violations
  - Test quality issues that hide bugs

Priority 3 — Suggestions (non-blocking):
  - Low / Info security findings
  - Naming style suggestions
  - Missing XML doc comments
  - Minor test quality suggestions (AAA labels, magic numbers)
  - Refactoring ideas not related to correctness
```

---

## Finding ID scheme

Use a consistent ID scheme across all findings so the author can reference them in replies:

```
[C-1], [C-2], ...   Conventions findings (Pass 1)
[S-1], [S-2], ...   Security findings (Pass 2)
[T-1], [T-2], ...   Test coverage findings (Pass 3)
```

---

## Tone guidelines

- **Be specific** — every finding links to a file and line number, not a vague description.
- **Show the fix** — include a corrected code snippet for every Must Fix and Should Fix finding.
- **Explain the why** — one sentence on why the rule exists, not just that it was violated.
- **Separate facts from suggestions** — use "This will cause X" for blockers and "Consider Y" for suggestions.
- **No personal criticism** — findings target the code, not the author. "This method" not "you wrote".
- **Acknowledge what is done well** — if the PR has solid test coverage or a particularly clean implementation of something, say so. Reviews that only list negatives are demoralizing and easy to dismiss.

---

## Compact verdict template (use when findings are few)

For small PRs with only 1–3 findings, the full table format can be replaced with:

```
**Verdict:** ⚠️ Approve with comments

**Findings:**
- [C-1] Should Fix — `OrderService.cs:42` — missing `sealed` on concrete class.
  Fix: change `public class OrderService` to `public sealed class OrderService`.

- [T-1] Should Fix — `POST /orders` has no `401 Unauthorized` test.
  Add: `POST_Orders_WhenUnauthenticated_Returns401()` in `CreateOrderTests.cs`.

**Passed:** Security (no findings) · Conventions (1 minor) · Test run (all green, 1 gap)
```

---

## Common verdict mistakes to avoid

| Mistake | Correct behaviour |
|---|---|
| Approving despite a failed test "because it's pre-existing" | Check if the PR caused the failure. If unsure, flag it. |
| Requesting changes for a Suggestion-only finding | Suggestions are non-blocking — use Approve with comments |
| Blocking for a missing XML doc comment | Missing docs are Should Fix at most, never a blocker |
| Ignoring a security finding because "it requires authentication to reach" | High findings on authenticated endpoints are still Request Changes |
| Giving a verdict without running (or asking for) test results | Always require test results before Approve or Approve with comments |
