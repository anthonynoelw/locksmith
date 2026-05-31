# Documentation Type Templates

Complete structure templates for every common documentation type.
Copy the skeleton, fill in the `[TODO: ...]` placeholders, and remove sections that don't apply.

---

## README (project root)

The README is the front door. It should answer four questions in order:
1. What is this?
2. How do I get it running?
3. What can I do with it?
4. Where do I go next?

```markdown
# Project Name

One sentence: what it does and for whom.

<!-- Optional: badges for build status, version, license -->
![Build](https://...) ![Version](https://...) ![License](https://...)

---

## What it does

2–4 sentences expanding on the one-liner. Focus on the problem it solves,
not how it works internally. End with one concrete outcome the user gets.

## Requirements

List only hard requirements — things that will cause failure if missing.

- [Runtime / language version]
- [Any required services or accounts]

## Get started

<!-- Show the fastest possible path to a working result — ideally < 5 commands -->

```bash
# 1. Install
npm install my-tool   # or: pip install / brew install / etc.

# 2. Configure
cp .env.example .env
# Edit .env and set YOUR_API_KEY

# 3. Run
my-tool start
```

[One sentence confirming what "success" looks like — e.g. "Open http://localhost:3000 to see the dashboard."]

## Usage

<!-- Show the most common use case first, then branch out -->

### [Most common task]

```bash
my-tool [command] [options]
```

[Brief explanation of what this does and what to look for in the output.]

### [Second most common task]

[Repeat the pattern: command → explanation → expected output]

## Configuration

| Variable | Required | Default | Description |
|---|---|---|---|
| `API_KEY` | Yes | — | Your API key from [service] |
| `PORT` | No | `3000` | Port the server listens on |
| `LOG_LEVEL` | No | `info` | One of: `debug`, `info`, `warn`, `error` |

## Learn more

- [Link to full docs or wiki]
- [Link to contributing guide]
- [Link to changelog]
- [Link to issue tracker]

## License

[License name] — see [LICENSE](./LICENSE)
```

---

## Getting started / quickstart guide

Goal: get the reader to their first successful result as fast as possible.
Every step must be doable. Do not skip steps that "seem obvious".

```markdown
# Get started with [Product]

By the end of this guide you'll have [concrete outcome — e.g. "a working API endpoint that returns user data"].

This takes about [N] minutes.

## Before you begin

You'll need:
- [Requirement 1] — [why / where to get it]
- [Requirement 2]

## Step 1: [Install / Set up]

```bash
[exact command]
```

You should see:
```
[expected output]
```

> **Trouble here?** [Link to troubleshooting or most common error + fix]

## Step 2: [Configure]

Create a file named `[filename]` with this content:

```yaml
[minimal working config]
```

Replace `[placeholder]` with your [what it is and where to find it].

## Step 3: [Run it]

```bash
[command]
```

[Confirmation of success — what to see, where to look]

## What's next?

You've got [thing] running. Here's where to go from here:

- [Link] — [one-line description of what this teaches/enables]
- [Link] — [one-line description]
- [Link] — [one-line description]
```

---

## API reference

Goal: help the reader use a specific method, endpoint, or option without reading anything else.
Be exhaustive but scannable. Every item follows the same structure.

```markdown
# [API / SDK] Reference

Quick links: [Method A](#method-a) · [Method B](#method-b) · [Method C](#method-c)

---

## [Method / Endpoint name]

[One sentence: what it does.]

```[language]
[Signature or HTTP method + path]
```

### Parameters

| Name | Type | Required | Default | Description |
|---|---|---|---|---|
| `param1` | `string` | Yes | — | [What it is. Valid values if limited.] |
| `param2` | `number` | No | `10` | [What it is. Min/max if relevant.] |

### Returns

[What comes back — type and what each field means, or link to the type definition.]

### Example

```[language]
[Minimal working example that produces a real result]
```

```[language]
// Response / output:
[expected result]
```

### Errors

| Code / Error | When it happens | Fix |
|---|---|---|
| `404 Not Found` | ID does not exist | Check the ID is correct |
| `ValidationError` | Required field missing | Include all required fields |

---

[Repeat for every method / endpoint]
```

---

## Configuration reference

Goal: answer "what does this option do and what should I set it to?"

```markdown
# Configuration Reference

[One sentence: where this config lives and how it's loaded.]

Config is read from (in order of priority, highest first):
1. Environment variables
2. `[config file name]`
3. Built-in defaults

---

## [Section / Group name]

### `OPTION_NAME` / `option.name`

[One sentence: what this controls.]

- **Type:** `string` | `number` | `boolean` | `[specific values]`
- **Default:** `[value]` (or "required — no default")
- **Environment variable:** `APP_OPTION_NAME`

```yaml
# Example
option:
  name: "value"
```

> **Note:** [Any important caveat — e.g. "Takes effect on next restart", "Only applies in production mode"]

[If the option has sub-options, list them with the same structure]

---

[Repeat per option or group]

## Full example config

```yaml
[Complete, copy-pasteable config file with all options set to their defaults
and inline comments explaining each one]
```
```

---

## Architecture / design doc

Goal: help a new team member or future-you understand why the system is built the way it is.
Focus on decisions and their reasons — not just a description of what exists.

```markdown
# [System / Feature] Architecture

**Last updated:** [date]
**Status:** [Draft | In review | Accepted | Superseded by [link]]

## Overview

[2–4 sentences: what this system does, what problem it solves, and what its boundaries are.]

## Context and constraints

What shaped the decisions below:

- [Constraint or requirement 1 — e.g. "Must handle 10k requests/second at peak"]
- [Constraint 2]
- [Existing dependency we can't change]

## Architecture diagram

```
[ASCII diagram or description of a diagram — boxes and arrows showing major components
and how data flows between them]
```

## Components

### [Component name]

**Responsibility:** [What it owns. One sentence.]

**Technology:** [What it's built with and why this was chosen over alternatives.]

**Interfaces:**
- Input: [what comes in and from where]
- Output: [what goes out and to where]

[Repeat for each major component]

## Key decisions

### [Decision title — e.g. "Use PostgreSQL instead of MongoDB"]

**Decision:** [What was decided]

**Why:** [The reason — what made this the right choice given the constraints]

**Trade-offs:** [What we gave up or what this makes harder]

**Alternatives considered:**
- [Alternative 1] — [Why rejected]
- [Alternative 2] — [Why rejected]

[Repeat for each significant architectural decision]

## What is not covered here

[Explicitly list what this doc does NOT cover — prevents readers from drawing wrong conclusions.]

## Open questions

- [ ] [Question that still needs an answer]
- [ ] [Decision not yet made]
```

---

## Contributing guide

Goal: help a new contributor make their first successful contribution without
needing to ask anyone.

```markdown
# Contributing to [Project]

Thanks for your interest in contributing. Here's everything you need to know
to get from idea to merged pull request.

## What kind of contributions are welcome?

- [Bug fixes] — always welcome
- [Feature requests] — please open an issue to discuss before building
- [Documentation improvements] — open a PR directly
- [Tests] — always welcome

## Set up your development environment

```bash
# 1. Fork and clone
git clone https://github.com/your-username/project.git
cd project

# 2. Install dependencies
[command]

# 3. Copy and configure
cp .env.example .env

# 4. Run tests to confirm everything works
[test command]
```

Expected output: `[what passing tests look like]`

## Make your change

1. Create a branch: `git checkout -b [type]/[short-description]`
   - `fix/` — bug fix
   - `feat/` — new feature
   - `docs/` — documentation only
   - `test/` — tests only

2. Make your changes.

3. Add or update tests. New features need tests. Bug fixes need a regression test.

4. Run the test suite: `[command]`. All tests must pass.

5. Run the linter: `[command]`. Fix any errors.

## Open a pull request

- **Title format:** `[type]: short description` — e.g. `fix: handle null values in parser`
- **Description:** What does this change? Why? Link to the issue it fixes (`Fixes #123`).
- **Keep PRs small.** One concern per PR. Large PRs are hard to review and slow to merge.

## Code review

- A maintainer will review within [timeframe].
- Address review comments with new commits — don't force-push during review.
- Once approved, a maintainer will merge.

## Code style

[Brief statement of style — e.g. "We follow the Airbnb style guide enforced by ESLint.
Run `npm run lint` before pushing."]

## Commit messages

[Brief statement — e.g. "We follow Conventional Commits: `type(scope): message`."]

## Questions?

[Where to ask — Slack channel, GitHub Discussions, email, etc.]
```

---

## Changelog

Goal: let users know what changed between versions and whether they need to take action.

```markdown
# Changelog

All notable changes are documented here.
Format follows [Keep a Changelog](https://keepachangelog.com).
Versions follow [Semantic Versioning](https://semver.org).

---

## [Unreleased]

### Added
- [New feature or capability]

### Changed
- [Behaviour that changed — include migration note if breaking]

### Fixed
- [Bug that was fixed]

---

## [1.2.0] — 2024-06-15

### Added
- Support for [feature] — [one sentence on what it enables]
- New `[option]` config option for [purpose]

### Changed
- `[method/endpoint]` now returns [new behaviour] instead of [old behaviour]

### Fixed
- Fixed crash when [condition] ([#123](link-to-issue))
- Corrected [thing] when [edge case]

### Removed
- Removed deprecated `[thing]` — use `[replacement]` instead

---

## [1.1.0] — 2024-05-01

[...same structure...]

---

## [1.0.0] — 2024-04-01

Initial release.
```
