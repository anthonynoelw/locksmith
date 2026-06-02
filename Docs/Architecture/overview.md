# Locksmith — Architecture Overview

Locksmith is an ASP.NET Core REST API that manages the full lifecycle of API keys:
issuance, authentication, rate limiting, rotation, and revocation.

## System purpose

[One paragraph: what problem does this solve and for whom?]

## Component overview

[A short description of each major layer: middleware pipeline, controllers,
services, EF Core context, and SQLite database.]

## Request lifecycle

[Describe what happens to a request from the moment it arrives at the server
to the moment a response is returned — including middleware execution order.]

## Security boundaries

[What data never leaves the server? Where does the pepper live? What is the
attack surface?]

## Technology decisions

[Link to the ADRs. Do not duplicate the reasoning here — just reference it.]
