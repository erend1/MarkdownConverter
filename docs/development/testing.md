# Testing Strategy

Use the cheapest test layer that can prove the behavior. Pure algorithms stay in fast unit tests; browser mechanics are verified at the browser boundary rather than through mocked JavaScript alone.

## Change-to-test routing

| Changed area | Required fast feedback | Additional evidence |
|---|---|---|
| Core contracts, models, orchestration | `MarkdownConverter.Core.Tests` | All consumers when a contract changes |
| Infrastructure parser/converter/renderer/DI | `MarkdownConverter.Infrastructure.Tests` | Golden/output assertions and affected format smoke checks |
| WebApp.Core presenters, view models, services | `MarkdownConverter.WebApp.Core.Tests` | Concurrency, cancellation, stale-state, and failure tests where applicable |
| Razor wiring and browser adapters | Closest pure-C# or adapter tests | Browser test for DOM, focus, selection, native undo, listener lifecycle, or rendering geometry |
| Desktop helpers and host contracts | `MarkdownConverter.Desktop.Tests` | Windows/WebView2 or endpoint smoke check when behavior crosses the helper boundary |
| Build, packaging, or release | Relevant local command | GitHub Actions run and artifact smoke check |

## Development loop

Run one affected project while iterating. Examples:

```powershell
dotnet test tests/MarkdownConverter.WebApp.Core.Tests/MarkdownConverter.WebApp.Core.Tests.csproj --configuration Release
dotnet test tests/MarkdownConverter.Infrastructure.Tests/MarkdownConverter.Infrastructure.Tests.csproj --configuration Release
```

Restore only when package/project inputs changed or the local cache is missing:

```powershell
dotnet restore MarkdownConverter.sln
```

Before requesting review, run the full solution:

```powershell
dotnet test MarkdownConverter.sln --configuration Release --no-restore --verbosity minimal
```

CI performs a clean restore, Release build, and complete test run. A local result does not replace CI.

## Test quality rules

- Test observable contracts and failure behavior, not private implementation structure.
- Keep tests deterministic; do not use arbitrary sleeps or wall-clock-fragile timing assertions.
- Use cancellation, controlled fakes, synchronization primitives, or observable-state polling for async behavior.
- Preserve meaningful existing cases. Change an assertion only when the approved contract changes, and explain why in the PR.
- A regression fix should fail before the fix when practical.
- Cover success, boundary, invalid input, dependency failure, cancellation, and stale/concurrent operation cases relevant to the change.
- Do not move algorithm coverage exclusively into slow browser tests.
- Do not hide flaky ownership or synchronization with broad retries.

## Browser and manual validation

The repository does not yet contain a Playwright suite. Until one is introduced through an approved issue, changes involving browser mechanics must document focused manual evidence. Browser automation becomes mandatory for scenarios covered by that suite once it is present.

Manual evidence must name the environment and exact behavior checked. “Tested manually” by itself is insufficient.

Desktop release smoke checks are defined in `.github/RELEASING.md` and remain required for release artifacts.

## Verification identity

Record the exact commit SHA with validation evidence. If `HEAD` changes after tests run, the result is stale and must not be presented as current. `Get-AgentContext.ps1 -Mode Verify` records the verified SHA under the ignored `.agent/` directory.
