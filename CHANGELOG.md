# Changelog

All notable changes to MarkdownConverter will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/), and this project follows [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

### Added

- Added the responsive Ink & Paper workspace with wide split panes, compact Source/Preview switching, pointer and keyboard resizing, a streamlined command bar, visible Find access at every width with touch-sized controls, dynamic viewport handling, and shared Web/Desktop rendering.

### Fixed

- Restored reliable Ctrl/Cmd+F and Ctrl/Cmd+H focus, explicit-range find navigation, strict replace-current behavior, scoped-search cleanup, native-undo replacement, and shortcut listener disposal in the shared WebApp editor.

## [0.1.0-alpha.2] - 2026-08-12

This patch prerelease corrects the portable Desktop packaging and file-open guidance from the first alpha.

### Fixed

- Included the Desktop application icon in portable release archives and documented exact-path Markdown file associations for cold and single-instance launches.

## [0.1.0-alpha.1] - 2026-08-11

This is the first public alpha prerelease of MarkdownConverter. It is intended for early validation rather than production-critical workflows.

### Release information

- **Desktop platform:** Windows 10/11 x64, distributed as a self-contained portable zip.
- **Runtime requirement:** Microsoft Edge WebView2 Runtime.
- **Source identity:** The tag-triggered release workflow appends the exact source commit to the published release notes.

### Added

- Added cost-conscious GitHub Pages deployment after the existing `build-test` job succeeds on `main`.
- Added third-party license notices to source, Pages, and Desktop release distributions.

### Changed

- Moved all production and test targets plus CI, Pages, and Desktop delivery to the .NET 10 LTS baseline.
- Updated the CI and release workflows to GitHub Actions versions that run on Node.js 24.

### Fixed

- Restored repository-scoped PWA installation, update, and offline navigation behavior for GitHub Pages.
- Removed standalone WebApp Desktop-capability probes that produced expected 404 responses on GitHub Pages.

### Security

- Updated WebAssembly and test dependencies to remove known vulnerable transitive packages.
- Replaced unauthenticated CDN loading with inventoried local KaTeX 0.16.47 and Lucide Static 0.460.0 assets, clearing the known KaTeX 0.16.9 advisory ranges.

### Known limitations

- The Desktop distribution is unsigned and does not include an installer or automatic updates.
- Desktop requires the WebView2 Runtime; compiled PDF export additionally requires a local LaTeX installation with the packages needed by the document.
- This alpha supports Windows x64 only. The standalone WebApp remains available separately through GitHub Pages.
