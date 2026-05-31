# Plain Language Reference

Rules for writing documentation that people actually read and understand.
Each rule has before/after examples so the pattern is unambiguous.

---

## Rule 1: Lead with the point

Put the most important information first. Don't make the reader get to the end of a paragraph to find out what it's about.

**Before:**
> Given the complexity of the authentication flow and the need to support multiple providers including OAuth2, SAML, and API keys, the system uses a pluggable identity middleware that can be configured per environment. This is called the Auth Manager.

**After:**
> The **Auth Manager** handles login across OAuth2, SAML, and API key providers. It's pluggable — swap providers without changing application code.

---

## Rule 2: One idea per sentence

Long sentences hide meaning. If a sentence needs a semicolon or three commas, split it.

**Before:**
> The migration tool reads your existing database schema, compares it against the target schema defined in your model files, generates the necessary SQL statements to reconcile the differences, and then optionally applies them after prompting for confirmation.

**After:**
> The migration tool reads your existing database schema and compares it against your model files. It generates the SQL needed to reconcile the differences. Run with `--apply` to execute after reviewing.

---

## Rule 3: Use active voice

Active voice is shorter and clearer. Passive voice hides who does what.

| Passive (avoid) | Active (prefer) |
|---|---|
| "The config file is read by the server at startup" | "The server reads the config file at startup" |
| "Errors are logged to stderr" | "The CLI logs errors to stderr" |
| "The token must be provided by the user" | "Provide your token as an environment variable" |
| "An exception will be thrown if the value is null" | "Throws `ArgumentNullException` if value is null" |

---

## Rule 4: Use "you" and direct address

Technical writing is a conversation. Talk to the reader.

**Before:**
> Users should ensure that the configuration file is present before attempting to run the application. If the configuration file is not found, the application will exit with an error.

**After:**
> Make sure your config file exists before running the application. If it's missing, the app exits with an error and tells you the expected path.

---

## Rule 5: Choose simple words

There is almost always a shorter, plainer word. Use it.

| Formal / technical | Plain |
|---|---|
| utilise | use |
| leverage | use |
| instantiate | create |
| invoke | call |
| terminate | stop / end |
| initialise | set up / start |
| facilitate | help / allow |
| subsequently | then |
| in order to | to |
| in the event that | if |
| prior to | before |
| with respect to | about / for |
| it is possible to | you can |
| it should be noted that | (delete — just say the thing) |

---

## Rule 6: Explain jargon the first time, then use it

If your audience includes people who don't know the term, define it once clearly — then use it freely.

**Before:**
> Configure the RBAC policies to restrict access based on the principal's claims.

**After:**
> Configure role-based access control (RBAC) policies to restrict what each user can do. RBAC works by checking the user's role (their "principal") against a list of allowed actions.

After introducing "RBAC" and "principal" this way, you can use them without re-explaining.

---

## Rule 7: Write for scanning

Most readers scan headings and code blocks before they commit to reading prose.
Make scanning productive.

**Heading rules:**
- Use H2 for major sections, H3 for sub-topics — don't go deeper unless you must
- Write headings as tasks or questions — not nouns
- Bad: `## Configuration` — Good: `## Configure your environment`
- Bad: `## Error handling` — Good: `## Handle errors gracefully`

**Code block rules:**
- Always use a language tag: ` ```bash `, ` ```js `, ` ```yaml ` — never a bare ` ``` `
- Show the simplest example that actually works — not the most complete one
- Add inline comments to explain non-obvious lines, not to describe obvious ones

**List rules:**
- Use bullets for unordered items (3–7 items ideal; more → use a table)
- Use numbers for ordered steps
- Each bullet completes the sentence the heading started — or is self-contained
- Don't mix sub-bullets more than one level deep

---

## Rule 8: Say what to do, not what not to do

Telling readers what to avoid forces them to hold the negative in mind. Tell them the positive action.

**Before:**
> Do not pass `null` as the value parameter. Do not call this method before initialising the client.

**After:**
> Pass a non-null string as `value`. Call this after `client.init()`.

The exception: genuine warnings about irreversible or dangerous actions should say exactly what not to do. "Do not run this command on a production database without a backup" is correct.

---

## Rule 9: Write consistent names

Pick one name for a thing and use it everywhere. Don't alternate between synonyms.

If the code calls it `ConnectionString`, the docs call it `ConnectionString` — not "connection URI", "database URL", or "db string". Synonyms create the impression that they're different things.

---

## Rule 10: Show realistic examples

Examples should use plausible real data, not placeholder nonsense.

| Placeholder (avoid) | Realistic (prefer) |
|---|---|
| `foo`, `bar`, `baz` | `order`, `customer`, `product` |
| `example.com` | `api.myapp.com` (if showing a real pattern) |
| `12345` | A realistic ID format for the system |
| `string value here` | An actual representative value |
| `TODO` in an example | Either a real value or `YOUR_API_KEY_HERE` (explicit placeholder) |

---

## Quick self-edit checklist

Read your draft and apply these in order:

1. **First sentence test** — does the first sentence of every section say what that section is about?
2. **Jargon scan** — circle every technical term. Is each one defined (once) or obviously known to your reader?
3. **Passive voice scan** — highlight every "is [verb]ed by" construction. Rewrite as active.
4. **Length scan** — any sentence over 25 words? Split it.
5. **Example check** — does every concept have a code example or concrete illustration?
6. **Heading check** — read only the headings. Does the structure of the document make sense from headings alone?
7. **Link check** — every link has descriptive text (not "here" or a bare URL)?
