---
name: docs
description: >
  Write best-practice markdown documentation files that are clear, human-readable,
  and easy to navigate. Trigger this skill whenever the user asks to write a README,
  create documentation, document a project, write a guide, create a how-to, write
  API docs, write contributing guidelines, write a changelog, create an architecture
  doc, document a CLI tool, write a wiki page, or says "document this", "write docs
  for", "create a README for", or "help me document". Also trigger when the user
  shares code or a project and asks how to explain it to others — even if they don't
  use the word "documentation". If someone wants other people to understand something,
  this skill applies.
---

# Markdown Documentation

Write documentation that real people actually read. Clear structure, plain language,
and just enough formatting — no more.

## Core principle

Every doc has one job: help the reader do something or understand something.
Write for that reader, not for completeness. If a section doesn't help them,
cut it.

---

## Before writing

Ask (or infer from context):

1. **Who is the reader?** — Developer unfamiliar with the project? End user? New team member? The answer changes everything about vocabulary and assumed knowledge.
2. **What is the doc's one job?** — Get started? Understand the architecture? Contribute? Reference an API?
3. **What doc type is this?** — See the type guide in `references/doc-types.md`.
4. **Is there existing content to work from?** — Code, comments, a rough draft, an old doc?

---

## Writing process

### 1. Open with the answer, not the preamble

The first sentence tells the reader what this thing is and what it does.
No history, no motivation, no "welcome to". If they're reading, they already care.

```markdown
<!-- WRONG — reader has to wait for the point -->
# MyApp

Welcome to MyApp! This project was created to solve the problem of...

<!-- CORRECT — immediate clarity -->
# MyApp

MyApp syncs your local `.env` files across machines using encrypted cloud storage.
```

### 2. Structure around tasks, not features

Readers come with a task in mind. Organise sections around what they want to do,
not around how the software is built.

```markdown
<!-- WRONG — organised around the product -->
## Configuration System
## Plugin Architecture
## Caching Layer

<!-- CORRECT — organised around the reader's tasks -->
## Get started in 5 minutes
## Configure for your environment
## Add your first plugin
## Improve performance with caching
```

### 3. Use plain language

Write as if explaining to a smart colleague who hasn't used this before.

| Instead of | Write |
|---|---|
| "Instantiate the client" | "Create a client" |
| "Invoke the method" | "Call the method" |
| "Leverage the API" | "Use the API" |
| "Terminate the process" | "Stop the process" |
| "Populate the required fields" | "Fill in the required fields" |

### 4. Show before you tell

Every concept gets an example. Code examples are more valuable than paragraphs.
Put the example first, then explain it.

```markdown
<!-- WRONG — explain then show -->
The `connect()` method establishes a connection to the server using
the credentials provided during initialisation. It returns a Promise
that resolves when the connection is established.

<!-- CORRECT — show then explain -->
```js
const client = new Client({ host: 'localhost', port: 5432 });
await client.connect();
```

`connect()` opens the connection using the credentials from the constructor.
It's async — `await` it before running queries.
```

### 5. Format to aid scanning, not to look thorough

Readers scan before they read. Use formatting to support that — but only where it genuinely helps.

**Use headings** to mark major sections (H2) and sub-sections (H3). Not every paragraph.

**Use bullet lists** for genuinely list-like things: requirements, options, steps with no order.
Do not use bullets to break up what should be prose.

**Use numbered lists** for steps that must happen in order.

**Use code blocks** for all code, commands, file paths, and config values — even one-liners.

**Use bold** for terms being defined and for the most important word in a warning.
Do not bold random phrases for emphasis.

**Use tables** for comparing options or showing parameter reference. Not for narrative content.

**Use blockquotes** (`>`) for callouts: tips, warnings, important notes. Label them.

```markdown
> **Note:** This only applies when running in production mode.

> **Warning:** This command deletes data permanently. There is no undo.
```

---

## Structure templates

Read `references/doc-types.md` for complete templates for:
- README (project root)
- Getting started / quickstart guide
- API reference
- Configuration reference
- Architecture / design doc
- Contributing guide
- Changelog

---

## Output format

Always produce the documentation as a complete, ready-to-use `.md` file.

After the file, add a short reviewer note (2–4 bullet points) covering:
- What was assumed about the reader
- Any section where real content is needed (marked `[TODO: ...]` in the file)
- One suggestion for what to add next

---

## Quality checklist

Before finishing, verify:

- [ ] First sentence says what the thing is and does — no warm-up
- [ ] Every section heading is a task or question, not a noun
- [ ] Every concept has a code example
- [ ] No jargon that the target reader wouldn't know
- [ ] No bullet list with more than 7 items (split or use a table instead)
- [ ] No heading followed immediately by another heading (add a sentence of context)
- [ ] All code blocks have a language tag (` ```js `, ` ```bash `, ` ```yaml `)
- [ ] File paths and commands are in backtick code spans
- [ ] Links are descriptive — not "click here" or bare URLs

---

## Reference files

| File | When to read |
|---|---|
| `references/doc-types.md` | Full templates for every documentation type |
| `references/plain-language.md` | Plain language rules and before/after examples |