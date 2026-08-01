# Human Leader Runbook for Planner and Worker Agents

This is the maintainer's operating guide for assigning, supervising, reviewing, and approving agent work in MarkdownConverter. The human leader controls priorities and approvals; agents obtain current repository state through the refresher instead of receiving a large copied context.

## The operating model

```text
Human chooses the outcome and priority
  -> Planner makes the issue Ready
  -> Human authorizes one worker
  -> Worker uses one worktree, branch, issue, and draft PR
  -> CI proves the exact commit
  -> Planner/reviewer checks the issue contract
  -> Human approves merge and later release
```

The human leader does not need to repeatedly explain the architecture or summarize earlier work. Give the agent a role, an issue number, and the correct worktree. `AGENTS.md`, the GitHub issue, the generated context, and the current diff supply the rest.

## Human responsibilities

The human leader owns:

- product direction and issue priority;
- user-visible behavior and trade-offs;
- approval of scope expansion or architecture exceptions;
- approval of new production dependencies and incompatible contracts;
- permission to push, create/edit GitHub state, merge, tag, release, or migrate the repository;
- final merge and release decisions;
- licensing and public contribution policy.

Agents may investigate and recommend. They must not silently make these decisions.

## Before using the workflow

The workflow files and refresher must exist on the default branch before new worktrees are created from `origin/main`. Verify that a newly created worktree contains `AGENTS.md` and the refresher before assigning an agent.

Required local tools:

- Git;
- .NET 8 SDK;
- PowerShell 7 or Windows PowerShell 5.1;
- GitHub CLI (`gh`) authenticated for online issue, PR, and Actions state.

The refresher remains useful offline, but offline state cannot authorize a start, review, or merge because remote dependencies may have changed.

## Step 1: choose and prepare an issue

Use an existing issue or ask a Planner to make one Ready. A Ready issue has evidence, one bounded outcome, scope/non-goals, dependencies, architecture constraints, numbered acceptance criteria, test requirements, risk, and refactor allowance.

Planner assignment prompt:

```text
Act as Planner for GitHub issue #<number>.

Read AGENTS.md and run:

./scripts/agent/Get-AgentContext.ps1 -Issue <number> -Mode Fast

Do not edit application code. Verify:
- dependency readiness;
- problem evidence and expected outcome;
- scope and non-goals;
- architecture invariants;
- numbered acceptance criteria;
- required unit, browser, integration, and manual tests;
- collision risk with active work;
- risk and refactor allowance.

Report whether the issue is Ready, Blocked, or Needs Replanning.
Do not create, edit, assign, or close GitHub state unless I explicitly
authorize that external action.
```

The human resolves any product decision the Planner identifies. Do not assign a Worker until the issue is Ready and its dependencies are closed.

## Step 2: refresh main and preflight the task

From the clean integration checkout:

```powershell
git switch main
git fetch origin
git pull --ff-only
./scripts/agent/Get-AgentContext.ps1 -Issue <number> -Mode Fast
```

On `main`, `NEEDS_ISSUE_BRANCH` is the expected result for a Ready issue. `BLOCKED`, `ISSUE_NOT_OPEN`, or `LOCAL_REMOTE_STALE` must be resolved before creating a Worker worktree.

Do not begin implementation in the main checkout.

## Step 3: create an isolated Worker worktree

Use one worktree and branch per issue:

```powershell
$issueNumber = 123
$issueSlug = "short-description"
git worktree add "..\MarkdownConverter-issue-$issueNumber" `
    -b "issue/$issueNumber-$issueSlug" `
    origin/main

Set-Location "..\MarkdownConverter-issue-$issueNumber"
```

Start the Worker with this directory as its working directory. Two workers must never share a checkout. The main checkout remains the clean reference/integration workspace.

## Step 4: assign the Worker

Copy this prompt and replace the issue number:

```text
Act as the Worker for GitHub issue #<number>.

First:
1. Read AGENTS.md.
2. Run:
   ./scripts/agent/Get-AgentContext.ps1 -Issue <number> -Mode Fast
3. Read .agent/context.md.
4. On this first load, read .agent/issue-<number>.md.
5. Read only the architecture/testing documents and source/tests relevant
   to issue #<number>.

If the refresher reports BLOCKED, ISSUE_NOT_OPEN,
BRANCH_TASK_MISMATCH, DIRTY_DEFAULT_BRANCH, or LOCAL_REMOTE_STALE,
resolve the safe local condition or report the blocker before editing.

Implement only issue #<number>. Preserve existing behavior and tests outside
its scope. Do not perform opportunistic cleanup, broad refactoring, or
dependency upgrades.

Run targeted tests while working and finish with:

./scripts/agent/Get-AgentContext.ps1 -Issue <number> -Mode Verify

Maintain the pull request handoff. Do not merge, tag, release, change
repository settings, or expand scope without my explicit approval.
```

The human supplies the goal and authority. The Worker discovers current state; do not paste the README, backlog, Git history, and earlier agent conversations into the prompt.

## Step 5: create the draft PR checkpoint

`NEEDS_DRAFT_PR` is expected on a new issue branch. A PR cannot normally be opened until the branch differs from main. After the first small, coherent, buildable commit:

1. Review the local diff.
2. Authorize the Worker to push/open the draft PR, or perform those actions yourself.
3. Use the repository PR template.
4. Record issue, branch, base SHA, current SHA, current state, completed work, one next action, decisions, verification, and blockers.

The draft PR description is the current handoff. Replace obsolete information instead of appending a chronological diary.

## Step 6: resume or replace a Worker

Use this prompt after an interruption or when a new Worker continues the same branch:

```text
Resume Worker task #<number> in this worktree.

Read AGENTS.md and run:

./scripts/agent/Get-AgentContext.ps1 -Issue <number> -Mode Fast

Use .agent/context.md as the state index. If this is your first load,
read the cached issue and PR descriptions. Otherwise, reread them when
their hash or updated time changed.

Inspect the diff from the recorded merge base and continue from the PR's
Current handoff section. Do not repeat completed investigation or broaden
the issue. Refresh/reintegrate main if the context reports stale remote state.
```

Run the refresher at these events—not on a timer:

- task start or resume;
- dependency merge;
- main-branch movement that affects the task;
- review feedback;
- before marking a PR ready;
- before final merge verification.

## Reading refresher output

| Readiness | Human/agent response |
|---|---|
| `READY` | Continue with the bounded task |
| `NEEDS_ISSUE_BRANCH` | Create the dedicated worktree/branch |
| `NEEDS_DRAFT_PR` | Reach the first coherent commit, then create the authorized draft PR |
| `BLOCKED` | Do not implement; resolve or wait for dependencies |
| `ISSUE_NOT_OPEN` | Stop and confirm whether a new/reopened issue is required |
| `BRANCH_TASK_MISMATCH` | Move the agent to the correct worktree |
| `DIRTY_DEFAULT_BRANCH` | Preserve the changes and move them off `main` |
| `LOCAL_REMOTE_STALE` | Fetch and safely integrate current main before continuing |
| `REMOTE_STATE_UNKNOWN` | Rerun online before start/review/merge decisions |
| `VERIFICATION_STALE` | Run required tests for the current worktree state |
| `CI_PENDING` | Wait for checks and refresh after their state changes |
| `CI_FAILED` | Fix the current commit before review |
| `READY_FOR_REVIEW` | Assign an independent Planner/reviewer |

## Generated context files

Every worktree has its own ignored `.agent/` cache:

| File | Purpose |
|---|---|
| `.agent/context.md` | Compact context for an agent or human |
| `.agent/state.json` | Detailed machine-readable state |
| `.agent/issue-<number>.md` | Current cached issue body |
| `.agent/pr-<number>.md` | Current cached PR description |
| `.agent/verification.json` | Last local verification and exact Git/worktree fingerprint |

These files are never authoritative and must not be committed. GitHub, Git, CI, and accepted ADRs remain the sources of truth.

## Step 7: final Worker verification

The Worker first runs the smallest relevant tests during implementation. On the final worktree state:

```powershell
./scripts/agent/Get-AgentContext.ps1 -Issue <number> -Mode Verify
```

The PR must state the exact verified SHA/worktree, targeted tests, full Release result, browser/Desktop/manual checks, and CI link. If code changes after verification, the recorded result becomes stale.

## Step 8: assign independent review

Reviewer prompt:

```text
Act as Planner/Reviewer for issue #<number> and its current pull request.

Do not implement changes. Read AGENTS.md and run:

./scripts/agent/Get-AgentContext.ps1 -Issue <number> -Mode Fast

Review the issue, PR handoff, base diff, and tests. Verify:
- every acceptance criterion maps to evidence;
- dependency direction and state ownership are correct;
- existing behavior and tests were preserved;
- failure, cancellation, concurrency, lifecycle, compatibility, and
  performance risks relevant to the issue are covered;
- the diff contains no unrelated refactoring or unexplained scope expansion;
- verification and CI belong to the reviewed commit.

Report Ready to Merge or list concrete findings. Do not merge or change
GitHub state unless I explicitly authorize it.
```

The Worker resolves code findings. The Planner/reviewer verifies the correction; they do not silently rewrite the issue contract during review.

## Step 9: human merge decision

Before approving merge, confirm:

- the issue is still open and dependencies remain satisfied;
- the PR closes the intended issue;
- acceptance evidence is complete;
- current full Release tests pass;
- required browser/Desktop/manual evidence is recorded;
- required CI checks are green for the reviewed commit;
- review conversations are resolved;
- the diff has no unrelated changes;
- documentation/changelog impact is handled;
- no unapproved dependency, license, release, or repository-setting change exists.

The human then approves merge. Code issues should close through `Closes #<number>` when the PR merges rather than through a separate early manual close.

## Parallel work limits

- One issue per Worker.
- One worktree and branch per issue.
- No shared mutable checkout.
- Start parallel Workers only when issue dependencies are closed and likely file/capability ownership does not collide.
- Use one integration Worker for cross-cutting convergence issues.
- Keep the human as the final priority and merge authority.

When coordination itself becomes expensive—several concurrent workers, frequent conflicts, or multiple repositories—introduce a small dispatcher. It should schedule ready work and detect conflicts, not duplicate Planner or Worker reasoning.

## Cost-control rules

- Give agents the issue number and role, not the complete repository narrative.
- Load the issue once per new agent and again only after its hash/update changes.
- Use `context.md` as an index and load only routed architecture, source, and test files.
- Use targeted tests during coding and one full verification for the final state.
- Refresh on state-changing events rather than periodically.
- Keep one current PR handoff instead of agent chat transcripts and progress diaries.

## Repository and publication boundary

Visibility, collaborator access, secrets, rulesets, licensing, and releases are separate human-approved operations. The workflow derives repository identity dynamically and continues to work when a remote URL or repository name changes.
