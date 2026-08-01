# Releasing and Deploying MarkdownConverter

Desktop releases are tag-driven. The standalone WebApp is deployed to GitHub Pages from verified `main` commits, and the Desktop host also copies the WebApp's `wwwroot` output into its own publish folder.

Both delivery workflows use the .NET 10 SDK and install the `wasm-tools` workload before restore. Use the same SDK and workload for local release verification.

## Channels

| Channel | Tag pattern | GitHub Release type | Use for |
|---------|-------------|---------------------|---------|
| Alpha | `v0.1.0-alpha.1` | Prerelease | Earliest validation builds |
| Beta | `v0.1.0-beta.1` | Prerelease | Feature-complete validation |
| Preview / RC | `v0.1.0-rc.1` | Prerelease | Release-candidate validation |
| Stable | `v0.1.0` | Release | General use |

## WebApp Deployment

The `ci` workflow deploys the WebApp after `build-test` succeeds for a push to `main` or a manual workflow dispatch. Pull requests build and test without deploying.

The source host page keeps `<base href="/" />` for local and Desktop hosting. The deployment job changes only the generated Pages artifact to use `/MarkdownConverter/`, adds `.nojekyll` so `_framework` assets are served, copies `index.html` to `404.html` for client-side route fallback, and includes the project and third-party notices.

The production URL is <https://erend1.github.io/MarkdownConverter/>. Treat the `github-pages` environment deployment attached to the exact commit as the authoritative publication result.

## Steps

1. Decide the target channel and version.
2. Add a `CHANGELOG.md` entry under `## [<version>] - <date>`.
3. Commit the changelog and any release-prep changes to `main`.
4. Push `main`.
5. Create and push the tag:

   ```bash
   git tag v<version>
   git push origin v<version>
   ```

6. Watch the `release` workflow under GitHub Actions.
7. Verify the GitHub Release has:
   - `MarkdownConverter-<version>-win-x64.zip`
   - `MarkdownConverter-<version>-win-x64.zip.sha256`
   - release notes copied from the matching changelog section
8. Verify the zip contains `LICENSE.txt` and `THIRD-PARTY-NOTICES.md`.
9. Download the zip on a clean Windows 10/11 machine with WebView2 Runtime installed and smoke-test:
   - app opens
   - new tab works
   - Markdown preview updates
   - save/open file works

## If Release Fails

If the workflow fails before creating a release, diagnose the failure on `main`. A tag that has never produced a public release may be deleted and recreated only with explicit maintainer approval:

```bash
git tag -d v<version>
git tag v<version>
git push origin :refs/tags/v<version>
git push origin v<version>
```

Never move or reuse a tag after its release has been published. Correct the problem and publish a new version instead. A partial release may be removed only through an explicit maintainer recovery decision.

## Required Repository Settings

These settings are not represented in source files and must be configured in GitHub:

1. Protect `main`.
2. Require the `build-test` status check from `.github/workflows/ci.yml` before merging.
3. Configure GitHub Pages to use GitHub Actions and restrict the `github-pages` environment to `main`.
4. Add a ruleset for tags matching `v*`.
5. Restrict `v*` tag creation/update/deletion to the repository owner or maintainer role.
6. Document the selected ruleset name and bypass roles in the PR description.

## Out of Scope

- Auto-update layers such as Velopack or Squirrel
- Code signing
- Installers such as MSIX, WiX, or Inno Setup
- Cross-platform builds
- NuGet package publishing
- Nightly or scheduled builds
