# Contributing to MarkdownConverter

MarkdownConverter uses a maintainer-directed workflow. Issues and evidence are welcome. Open or obtain approval for a Ready issue before submitting code; unsolicited pull requests may be closed without review.

## Before making a change

1. Start from an approved, ready GitHub issue.
2. Read [AGENTS.md](AGENTS.md), even when working manually.
3. Confirm issue dependencies and architecture constraints.
4. Create one issue branch and, when multiple workers are active, a separate Git worktree.
5. Open a draft pull request early and keep its description current.

Do not start implementation while required product behavior or acceptance criteria remain undecided.

## Build and test

Requirements and application commands are documented in [README.md](README.md). The standard validation commands are:

```powershell
dotnet restore MarkdownConverter.sln
dotnet build MarkdownConverter.sln --configuration Release --no-restore
dotnet test MarkdownConverter.sln --configuration Release --no-build --no-restore --verbosity minimal
```

During implementation, use the smallest relevant test project for fast feedback. See [docs/development/testing.md](docs/development/testing.md) for the change-to-test mapping and browser/manual expectations.

## Branches and pull requests

Use a descriptive issue branch such as:

```text
issue/123-short-description
```

A pull request must:

- link and normally close exactly one issue;
- explain the behavioral outcome and deliberately unchanged behavior;
- map acceptance criteria to evidence;
- identify architecture or persisted-contract impact;
- report exact validation commands and the verified commit;
- contain no unrelated refactoring or dependency upgrades;
- retain all meaningful existing tests.

Use the repository pull request template and keep its handoff section updated in place.

## Commit and scope discipline

- Prefer small, reviewable commits that each leave the branch buildable.
- Keep generated files, build output, local agent context, credentials, and machine-specific settings out of Git.
- Use your own identity for commits and preserve copyright or license notices already present in files you change.
- Raise a separate issue for useful cleanup that is not necessary for the current outcome.
- Never rewrite shared history, force-push a protected branch, merge, tag, or release without maintainer approval.

## Licensing and external contributions

The project is licensed under the [Apache License 2.0](LICENSE). By intentionally submitting a contribution for inclusion, you agree that it may be distributed under that license.

Do not add third-party code, generated assets, fonts, or packages whose license is incompatible or unclear. Record required notices and obtain maintainer approval before adding a production dependency.

Repository visibility does not grant merge, release, or settings authority. The maintainer retains final control over accepted changes and publication.
