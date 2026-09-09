# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

Generated from [Conventional Commits](https://www.conventionalcommits.org/) via [release-please](https://github.com/googleapis/release-please).

## [1.1.0](https://github.com/anthonynoelw/locksmith/compare/v1.0.0...v1.1.0) (2026-07-23)


### Features

* add API key action permission endpoints ([#2](https://github.com/anthonynoelw/locksmith/issues/2)) ([145a6cf](https://github.com/anthonynoelw/locksmith/commit/145a6cf75d6a0080bae193447ce48a7391f6c1df))
* add API key creation endpoint and cryptographic key generation ([a3e22d4](https://github.com/anthonynoelw/locksmith/commit/a3e22d48c746bdb104ba0af43d16bc57120236ee))
* add API key rotate and delete endpoints ([#4](https://github.com/anthonynoelw/locksmith/issues/4)) ([345ca3b](https://github.com/anthonynoelw/locksmith/commit/345ca3b4275f18b31443bbabb2ae5f1fdc1f7aa6))
* add data access layer with repository pattern and UnitOfWork ([9affd3e](https://github.com/anthonynoelw/locksmith/commit/9affd3efccf2a17802fa0eb056a482a34ebcd5aa))
* add database migrations for soft-delete and append-only constraints ([d31818c](https://github.com/anthonynoelw/locksmith/commit/d31818c54bc361a9cd517d934549051631ce9f9f))
* add Infrastructure service registration and database settings ([5ca44e8](https://github.com/anthonynoelw/locksmith/commit/5ca44e82629ca91f622fe64cf74d512cb30aa539))
* added migrations ([7e9847d](https://github.com/anthonynoelw/locksmith/commit/7e9847d8d65efcdb7ba5129518605aad17e6777b))
* added scalar ui & updated docs ([5895621](https://github.com/anthonynoelw/locksmith/commit/5895621441ddbaebc1c9eee2f4509741d5a0c50c))
* API key status endpoints + ExecuteAsync naming refactor ([#1](https://github.com/anthonynoelw/locksmith/issues/1)) ([fa7b5f0](https://github.com/anthonynoelw/locksmith/commit/fa7b5f08678404fafcfc877d3ebbe50377f71481))
* authentication with static bearer token ([8b34cb9](https://github.com/anthonynoelw/locksmith/commit/8b34cb9dc2b5ba55d2e052146eaf9b407ce18ecc))
* configure API and Agent entry points with DI and middleware ([05eec14](https://github.com/anthonynoelw/locksmith/commit/05eec146ecbd8e7e315eea958479179c6d6d8fea))
* enhance domain models and configure EF Core DbContext ([d8ed799](https://github.com/anthonynoelw/locksmith/commit/d8ed7990d49d5f161b192f4587775dc4388ed7d6))
* implement idempotency key table and repository pattern ([8e58dc4](https://github.com/anthonynoelw/locksmith/commit/8e58dc43fa2df6d0671fc5df302a6938ee047766))
* implement service layer for API key retrieval operations ([6ee4ca2](https://github.com/anthonynoelw/locksmith/commit/6ee4ca26e8d067e432144a1db46a9e7e513d7fd1))
* setup Redis with distributed caching and health checks ([44fc5e7](https://github.com/anthonynoelw/locksmith/commit/44fc5e7cbf61a2b5a1ae7deea93a1852a5dbe9b5))
* wire EF Core readiness health check tagged "ready" ([e42c06e](https://github.com/anthonynoelw/locksmith/commit/e42c06e8f72369991cf6b162418cfce6a04e4126))


### Bug Fixes

* application tests for ci ([0de677b](https://github.com/anthonynoelw/locksmith/commit/0de677befa828b959303681e95fe6cbd88f96e1e))
* docker files ([2a4adab](https://github.com/anthonynoelw/locksmith/commit/2a4adab9c06355dd38321047dc61f8a17f8f83d4))
* removed unecessary settings from  .env.example ([4632d38](https://github.com/anthonynoelw/locksmith/commit/4632d38f5e157b04c941a319116d771741c52380))
* sln file ([920a5cc](https://github.com/anthonynoelw/locksmith/commit/920a5cc90848513091a51ee2da23f42ba26b25f8))
* updated appsettings to match projects ([594c522](https://github.com/anthonynoelw/locksmith/commit/594c5226664cca04ea1a7cd766ee59f79b04d217))
* updated editorconfig for using directive ([abd8f4e](https://github.com/anthonynoelw/locksmith/commit/abd8f4ebfd8770bec741429768ac1d9e4007c394))

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
