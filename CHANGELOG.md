# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

Generated from [Conventional Commits](https://www.conventionalcommits.org/) via [release-please](https://github.com/googleapis/release-please).

## [Unreleased]

### Added
- API versioning with tests
- Structured logging with Serilog in Api and Agent
- GitHub Actions CI/CD pipeline
- Integration and Application test projects
- Global exception handling with RFC 9457 Problem Details

### Changed
- Re-evaluated project features and template structure
- Relocated exception handler tests to Application project
- Adopted FluentAssertions for test assertions

## Development Notes

Releases follow [Semantic Versioning](https://semver.org/):
- **Major** (X.0.0): Breaking changes
- **Minor** (0.X.0): New features (backwards compatible)
- **Patch** (0.0.X): Bug fixes

Version bumping and changelog updates are automated via conventional commits:
- `feat:` → Minor bump
- `fix:` → Patch bump
- `BREAKING CHANGE:` → Major bump
