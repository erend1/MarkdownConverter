# Agent Workflow and State Refresh

This workflow lets planners and workers operate asynchronously without duplicating or repeatedly reloading the entire repository state.

Human maintainers assigning and approving agents should follow the step-by-step [Human Leader Runbook](human-leader-runbook.md).

## Canonical state ownership

| State | Canonical location |
|---|---|
| Product priority and scheduling | GitHub Project/milestone maintained by the human lead |
| Required behavior, scope, dependencies | GitHub issue |
| Active implementation and handoff | Issue branch and draft PR description |
| Code and history | Git commit/diff |
| Verification | Local/CI result tied to an exact SHA |
| Durable architecture decision | ADR |
| Release history | Changelog, tag, GitHub release |
| Cached context | Generated `.agent/` files; never authoritative |

Do not commit a project status snapshot. Volatile state must have one owner and be queried when needed.

## State machine

```text
Idea
  -> planner investigation
  -> Ready (Definition of Ready satisfied)
  -> Claimed (one worker, branch, base SHA)
  -> Draft PR / In progress
  -> Ready for review
  -> Planner/reviewer verification
  -> Human merge approval
  -> Merged / issue closed
```

Use one status system—prefer a GitHub Project field. Labels describe type, area, and risk; they should not duplicate project status.

## Fast refresh

Run:

```powershell
./scripts/agent/Get-AgentContext.ps1 -Issue <number> -Mode Fast
```

The refresher writes compact generated files under `.agent/` and prints `context.md`. It derives repository identity from Git/GitHub so owner and repository names are never hardcoded.

Fast mode collects:

- branch, HEAD, merge base, tracking-main and remote-main SHAs;
- ahead/behind and dirty/changed files;
- task metadata, issue-body fingerprint, and explicit dependency states;
- current branch PR, review/check summary, and latest workflow run;
- static-context fingerprint;
- last verification SHA and whether it is current;
- one deterministic next action and warnings.

Use `-Offline` to skip GitHub and remote calls. An offline snapshot is intentionally marked potentially stale.

CI runs Fast/Offline mode on every push and pull request so parser or cross-platform PowerShell regressions fail before merge. Network-dependent issue and PR collection remains a local/agent integration check rather than a CI dependency.

Only reread large context when its key changes:

| Key | Reload when changed |
|---|---|
| Remote/main SHA | Recent commits, base diff, affected architecture/tests |
| Issue `updatedAt` or body hash | Issue contract and dependencies |
| PR `updatedAt` | Handoff, reviews, and checks |
| Static-context hash | `AGENTS.md` and routed guardrail/testing documents |
| Verified SHA | Test evidence |

Refresh at task start/resume, after a dependency merge, after review feedback, before marking a PR ready, and before merge verification. Do not poll merely because time passed.

## Isolated work

Each worker owns one issue, branch, and worktree. A typical setup is:

```powershell
$issueNumber = 123
$issueSlug = "short-description"
git fetch origin
git worktree add "..\MarkdownConverter-issue-$issueNumber" -b "issue/$issueNumber-$issueSlug" origin/main
```

The main checkout should remain a clean integration/reference workspace. Record the starting base SHA in the draft PR. If remote main changes, refresh and integrate it before presenting final verification.

Planners may prepare non-overlapping future issues while a worker codes. They must not edit the worker's files or change acceptance criteria silently. Concurrent worker lanes require explicit file/capability ownership and satisfied issue dependencies.

## Definition of Ready

An issue is Ready only when it has:

- evidence or a reproducible problem;
- one clear observable outcome;
- explicit in-scope and out-of-scope behavior;
- satisfied dependencies and known downstream blockers;
- architecture invariants and compatibility constraints;
- numbered, verifiable acceptance criteria;
- existing tests to preserve and required new test cases;
- browser/manual evidence where unit tests cannot prove behavior;
- risk, likely collision surface, and refactor allowance;
- no unresolved product decision.

Separate mandatory behavior from suggested implementation. Class names and mechanisms should be requirements only when compatibility or architecture truly depends on them.

## Worker loop

1. Refresh state and verify the issue is Ready.
2. Claim it with an assignee/branch/base SHA and open a draft PR.
3. Run the smallest relevant baseline tests.
4. Implement one cohesive behavioral slice at the existing ownership seam.
5. Run targeted tests during iteration.
6. Update the PR snapshot when the state, decision, blocker, or next action changes.
7. Run full Release verification for the final commit.
8. Map every acceptance criterion to evidence and request review.

If the solution needs an unexpected project, dependency, public contract change, persistence migration, or substantially larger diff, stop and request replanning.

## Bounded handoff

Keep this in the draft PR description and replace obsolete content:

```text
Issue:
Branch / base SHA / current SHA:
State:
Completed:
Next exact action:
Decisions and preserved invariants:
Files/capabilities changed:
Verification and verified SHA:
Blockers/questions:
Approved follow-ups:
```

Do not copy chat transcripts or chronological activity logs into the repository.

## Review and completion

The planner/reviewer checks:

- acceptance-criterion traceability;
- architecture direction and state ownership;
- regression, concurrency, cancellation, lifecycle, and compatibility risks;
- whether tests use the correct layer and preserve existing behavior;
- diff scope and absence of incidental refactoring;
- current local/CI evidence for the reviewed SHA.

The worker resolves code findings. The human lead decides unresolved trade-offs and approves merge. Use `Closes #<issue>` so a code issue closes on merge rather than through a separate, potentially stale manual action.

## Repository identity and publication controls

- Do not hardcode an owner/repository name in scripts or documentation.
- Keep repository links relative where possible.
- Do not import unrelated repository history or external task state without explicit human approval.
- Treat visibility, collaborator access, secrets, rulesets, licensing, and releases as separately approved human operations.
