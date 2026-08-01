# Changelog

All notable changes to MarkdownConverter will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/), and this project follows [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

### Added

- Added cost-conscious GitHub Pages deployment after the existing `build-test` job succeeds on `main`.
- Added third-party license notices to source, Pages, and Desktop release distributions.

### Changed

- Updated the CI and release workflows to GitHub Actions versions that run on Node.js 24.

### Security

- Updated the .NET 8 WebAssembly and test dependencies to remove known vulnerable transitive packages.

The first published version will receive a dated section when its release candidate is approved.
