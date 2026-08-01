# MarkdownConverter Agent Contract

This file applies to the entire repository. It is the compact, stable entry point for human and automated contributors. Volatile task status belongs in GitHub issues, pull requests, commits, and CI—not in this file or in chat history.

## Source of truth

Use these sources in this order:

1. The assigned GitHub issue defines required behavior, scope, acceptance criteria, and dependencies.
2. The checked-out commit and its diff define the implementation state.
3. CI results tied to that exact commit define remote verification state.
4. Pull request descriptions contain the current worker handoff.
5. Architecture decision records contain durable cross-cutting decisions.
6. Generated `.agent/` files are disposable indexes only. Never treat them as authoritative.

Do not use old agent conversations, manually maintained status documents, or test results from another commit as current state.

## Start or resume work

1. Run `./scripts/agent/Get-AgentContext.ps1 -Issue <number> -Mode Fast`.
2. Confirm the branch, base SHA, remote-main SHA, worktree status, issue state, dependencies, and last verified SHA.
3. Read the issue body only when starting the task or when its body hash/`updatedAt` value changed.
4. Read only the architecture and testing documents routed by the affected area.
5. Work in an isolated worktree and one issue branch. Do not implement on `main`.
6. Establish a targeted test baseline before changing behavior.

Use `-Offline` when network access is unavailable. Offline output must be treated as potentially stale until a remote refresh succeeds.

## Roles

### Human lead

- Owns product priority, user-visible trade-offs, architecture exceptions, merge approval, licensing, repository administration, and releases.
- Must approve new production dependencies, public contract breaks, destructive migrations, and expanded issue scope.

### Planner

- Investigates evidence and the smallest correct behavioral outcome.
- Defines dependencies, invariants, acceptance criteria, test requirements, risk, non-goals, and refactor allowance.
- Reviews the finished PR against the issue contract but does not silently change that contract during review.
- Does not implement concurrently in a worker's branch.

### Worker

- Owns one issue, one branch, one worktree, and one draft PR.
- Implements the smallest cohesive solution that satisfies the issue.
- Adds tests at the cheapest layer capable of proving the behavior.
- Maintains the PR description as the latest structured handoff.
- Stops and requests replanning when required scope materially expands.

The worker that authored a change must not treat self-review as independent approval. CI is required evidence, not a substitute for review.

## Architecture invariants

Read [docs/architecture/guardrails.md](docs/architecture/guardrails.md) before any cross-project or dependency-boundary change.

- `MarkdownConverter.Core` has no project or NuGet dependencies.
- `MarkdownConverter.Infrastructure` implements Core abstractions and contains external packages.
- `MarkdownConverter.WebApp.Core` is pure C#, depends only on Core, and contains PVM state and behavior without Blazor or JavaScript dependencies.
- `MarkdownConverter.WebApp` and `MarkdownConverter.Desktop` are outer composition/adaptation layers.
- Browser mechanics may remain in JavaScript; application decisions, state, and testable algorithms belong in C#.
- Composition roots own DI registration. Production code must not use a service locator.
- Preserve cancellation, typed outcomes, error visibility, native editor undo, and platform-specific capability boundaries.

Large files, old code, naming preferences, and nearby cleanup are not sufficient reasons to refactor.

## Change discipline

- Preserve user changes and unrelated worktree changes.
- Do not perform opportunistic renames, formatting sweeps, package upgrades, or unrelated cleanup.
- Do not weaken, skip, or delete existing tests to make a change pass.
- Add an abstraction only when the issue requires a real ownership, dependency, or testability boundary.
- Keep public and persisted contracts compatible unless the issue explicitly authorizes a change and migration.
- Record discovered follow-up work as a separate issue; do not bundle it into the active PR.
- If unexpected projects or high-conflict files must change, pause and update the issue/PR scope before continuing.
- Do not merge, push tags, publish releases, change repository settings, or choose a license without explicit human authorization.

## Testing

Follow [docs/development/testing.md](docs/development/testing.md).

During development, run the smallest relevant test project. Before review, run:

```powershell
dotnet test MarkdownConverter.sln --configuration Release --no-restore --verbosity minimal
```

Or use:

```powershell
./scripts/agent/Get-AgentContext.ps1 -Issue <number> -Mode Verify
```

A test result is current only when its recorded SHA equals `HEAD`. UI behavior that crosses the browser boundary also requires the browser/manual evidence defined by the issue.

## Pull request handoff

Open a draft PR early. Keep one bounded, current snapshot in its description:

- issue, branch, base SHA, and current SHA;
- current state and completed work;
- one exact next action;
- decisions and preserved invariants;
- files or boundaries changed;
- tests and manual checks, including verified SHA;
- blockers, questions, and approved follow-ups.

Do not accumulate a progress diary. Replace obsolete handoff information.

## Definition of done

A change is done only when:

- every acceptance criterion maps to code, a test, or explicit manual evidence;
- existing and required new tests pass in Release configuration;
- CI is green for the current commit;
- the diff contains no unrelated changes or unexplained scope expansion;
- relevant documentation and changelog entries are updated when required;
- the planner/reviewer has checked architecture, regression risk, and test sufficiency;
- the human merge gate is satisfied.

## Escalate instead of assuming

Stop and request a human decision for ambiguous product behavior, data-loss risk, security/privacy impact, licensing, a new external dependency, public API or persistence incompatibility, release changes, or a materially larger solution than the issue describes.

Scripts and documentation must derive the GitHub owner/repository dynamically and must not hardcode a remote identity.
