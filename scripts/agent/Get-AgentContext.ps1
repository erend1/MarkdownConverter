#requires -Version 5.1

<#
.SYNOPSIS
Builds a compact, disposable context snapshot for planner and worker agents.

.DESCRIPTION
The script derives repository identity from Git/GitHub, records local and
remote revision state, summarizes one issue and the current branch pull
request, fingerprints stable repository instructions, and tracks verification
against an exact Git/worktree state.

Generated output is written below .agent/ by default and is never an
authoritative project-state source.

.PARAMETER IssueNumber
GitHub issue to summarize. Alias: -Issue. When omitted, the script attempts to
infer an issue number from a branch segment such as issue/18-description.

.PARAMETER Mode
Fast performs read-only state collection. Verify additionally runs the full
Release test suite and records the result.

.PARAMETER Offline
Skips GitHub CLI and remote-network calls. Local tracking references may be
stale and the resulting context is marked accordingly.

.PARAMETER RemoteName
Git remote used for the default branch. Defaults to origin.

.PARAMETER OutputDirectory
Generated-state directory. Relative paths are resolved from the repository
root. Defaults to .agent.

.EXAMPLE
./scripts/agent/Get-AgentContext.ps1 -Issue 123 -Mode Fast

.EXAMPLE
./scripts/agent/Get-AgentContext.ps1 -Issue 123 -Mode Verify

.EXAMPLE
./scripts/agent/Get-AgentContext.ps1 -Issue 123 -Offline
#>

[CmdletBinding()]
param(
    [Alias("Issue")]
    [ValidateRange(0, 2147483647)]
    [int]$IssueNumber = 0,

    [ValidateSet("Fast", "Verify")]
    [string]$Mode = "Fast",

    [switch]$Offline,

    [ValidateNotNullOrEmpty()]
    [string]$RemoteName = "origin",

    [string]$OutputDirectory
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

function Invoke-NativeCommand {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory = $true)]
        [string]$FilePath,

        [string[]]$ArgumentList = @(),

        [switch]$AllowFailure
    )

    if (-not (Get-Command $FilePath -ErrorAction SilentlyContinue)) {
        $message = "Required command '$FilePath' was not found on PATH."
        if (-not $AllowFailure) {
            throw $message
        }

        return [pscustomobject]@{
            ExitCode = 127
            Lines = @($message)
            Text = $message
        }
    }

    Write-Verbose ("Running: {0} {1}" -f $FilePath, ($ArgumentList -join " "))
    # Windows PowerShell 5.1 wraps native stderr as ErrorRecord objects and
    # honors the caller's Stop preference before we can inspect LASTEXITCODE.
    # Native tools legitimately emit warnings on stderr with exit code 0, so
    # temporarily collect both streams and make the exit code authoritative.
    $previousErrorActionPreference = $ErrorActionPreference
    $ErrorActionPreference = "Continue"
    try {
        $nativeOutput = @(& $FilePath @ArgumentList 2>&1)
        $exitCode = $LASTEXITCODE
    }
    finally {
        $ErrorActionPreference = $previousErrorActionPreference
    }
    $lines = @($nativeOutput | ForEach-Object { $_.ToString() })
    $text = ($lines -join [Environment]::NewLine).Trim()

    if ($exitCode -ne 0 -and -not $AllowFailure) {
        throw "Command failed with exit code ${exitCode}: $FilePath $($ArgumentList -join ' ')`n$text"
    }

    return [pscustomobject]@{
        ExitCode = $exitCode
        Lines = $lines
        Text = $text
    }
}

function Invoke-Git {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory = $true)]
        [string[]]$ArgumentList,

        [switch]$AllowFailure
    )

    return Invoke-NativeCommand -FilePath "git" -ArgumentList $ArgumentList -AllowFailure:$AllowFailure
}

function Get-OptionalProperty {
    param(
        [AllowNull()]
        [object]$InputObject,

        [Parameter(Mandatory = $true)]
        [string]$Name,

        [AllowNull()]
        [object]$DefaultValue = $null
    )

    if ($null -eq $InputObject) {
        return $DefaultValue
    }

    $property = $InputObject.PSObject.Properties[$Name]
    if ($null -eq $property) {
        return $DefaultValue
    }

    return $property.Value
}

function ConvertFrom-JsonSafe {
    param(
        [AllowEmptyString()]
        [string]$Json,

        [Parameter(Mandatory = $true)]
        [string]$Description
    )

    if ([string]::IsNullOrWhiteSpace($Json)) {
        throw "$Description returned no JSON."
    }

    try {
        return $Json | ConvertFrom-Json
    }
    catch {
        throw "$Description returned invalid JSON: $($_.Exception.Message)"
    }
}

function Get-Sha256Text {
    param([AllowEmptyString()][string]$Text)

    $algorithm = [System.Security.Cryptography.SHA256]::Create()
    try {
        $bytes = [System.Text.Encoding]::UTF8.GetBytes($Text)
        $hash = $algorithm.ComputeHash($bytes)
        return ([System.BitConverter]::ToString($hash)).Replace("-", "").ToLowerInvariant()
    }
    finally {
        $algorithm.Dispose()
    }
}

function ConvertTo-CanonicalTimestamp {
    param([AllowNull()][object]$Value)

    if ($null -eq $Value) {
        return $null
    }

    $format = "yyyy-MM-dd'T'HH:mm:ss.fffffff'Z'"
    if ($Value -is [DateTime]) {
        return $Value.ToUniversalTime().ToString($format, [System.Globalization.CultureInfo]::InvariantCulture)
    }

    if ($Value -is [DateTimeOffset]) {
        return $Value.ToUniversalTime().ToString($format, [System.Globalization.CultureInfo]::InvariantCulture)
    }

    $parsed = [DateTimeOffset]::MinValue
    $styles = [System.Globalization.DateTimeStyles]::AssumeUniversal -bor
        [System.Globalization.DateTimeStyles]::AdjustToUniversal
    if ([DateTimeOffset]::TryParse(
        [string]$Value,
        [System.Globalization.CultureInfo]::InvariantCulture,
        $styles,
        [ref]$parsed)) {
        return $parsed.ToUniversalTime().ToString($format, [System.Globalization.CultureInfo]::InvariantCulture)
    }

    return [string]$Value
}

function Write-Utf8File {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Path,

        [AllowEmptyString()]
        [string]$Content
    )

    $encoding = New-Object System.Text.UTF8Encoding($false)
    [System.IO.File]::WriteAllText($Path, $Content, $encoding)
}

function Get-RepositoryRelativePath {
    param(
        [Parameter(Mandatory = $true)]
        [string]$RepositoryRoot,

        [Parameter(Mandatory = $true)]
        [string]$FullPath
    )

    $relative = $FullPath.Substring($RepositoryRoot.Length)
    return $relative.TrimStart([char]'\', [char]'/').Replace('\', '/')
}

function Get-StaticContextFingerprint {
    param([Parameter(Mandatory = $true)][string]$RepositoryRoot)

    $relativePaths = New-Object System.Collections.Generic.List[string]
    $fixedPaths = @(
        "AGENTS.md",
        "CONTRIBUTING.md",
        "Directory.Build.props",
        "MarkdownConverter.sln",
        "README.md",
        "Directory.Packages.props",
        "global.json"
    )

    foreach ($relativePath in $fixedPaths) {
        if (Test-Path -LiteralPath (Join-Path $RepositoryRoot $relativePath)) {
            $relativePaths.Add($relativePath.Replace('\', '/')) | Out-Null
        }
    }

    $searchRoots = @(
        (Join-Path $RepositoryRoot "src"),
        (Join-Path $RepositoryRoot "tests")
    )
    foreach ($searchRoot in $searchRoots) {
        if (-not (Test-Path -LiteralPath $searchRoot)) {
            continue
        }

        Get-ChildItem -LiteralPath $searchRoot -Recurse -File -Filter "*.csproj" |
            ForEach-Object {
                $relativePaths.Add((Get-RepositoryRelativePath -RepositoryRoot $RepositoryRoot -FullPath $_.FullName)) | Out-Null
            }
    }

    $documentationRoots = @(
        (Join-Path $RepositoryRoot "docs\architecture"),
        (Join-Path $RepositoryRoot "docs\development")
    )
    foreach ($documentationRoot in $documentationRoots) {
        if (-not (Test-Path -LiteralPath $documentationRoot)) {
            continue
        }

        Get-ChildItem -LiteralPath $documentationRoot -Recurse -File -Filter "*.md" |
            ForEach-Object {
                $relativePaths.Add((Get-RepositoryRelativePath -RepositoryRoot $RepositoryRoot -FullPath $_.FullName)) | Out-Null
            }
    }

    $githubRoot = Join-Path $RepositoryRoot ".github"
    if (Test-Path -LiteralPath $githubRoot) {
        Get-ChildItem -LiteralPath $githubRoot -Recurse -File |
            Where-Object { $_.Extension -in @(".yml", ".yaml", ".md") } |
            ForEach-Object {
                $relativePaths.Add((Get-RepositoryRelativePath -RepositoryRoot $RepositoryRoot -FullPath $_.FullName)) | Out-Null
            }
    }

    $entries = New-Object System.Collections.Generic.List[string]
    $uniquePaths = @($relativePaths | Sort-Object -Unique)
    foreach ($relativePath in $uniquePaths) {
        $fullPath = Join-Path $RepositoryRoot $relativePath
        $fileHash = (Get-FileHash -LiteralPath $fullPath -Algorithm SHA256).Hash.ToLowerInvariant()
        $entries.Add("${relativePath}:${fileHash}") | Out-Null
    }

    return [pscustomobject]@{
        Hash = Get-Sha256Text -Text ($entries -join "`n")
        Files = $uniquePaths
    }
}

function Get-WorktreeFingerprint {
    param([Parameter(Mandatory = $true)][string]$RepositoryRoot)

    $parts = New-Object System.Collections.Generic.List[string]
    $diffResult = Invoke-Git -ArgumentList @("diff", "--no-ext-diff", "HEAD") -AllowFailure
    if ($diffResult.ExitCode -eq 0) {
        $parts.Add($diffResult.Text) | Out-Null
    }

    $untrackedResult = Invoke-Git -ArgumentList @("ls-files", "--others", "--exclude-standard") -AllowFailure
    if ($untrackedResult.ExitCode -eq 0) {
        foreach ($relativePath in @($untrackedResult.Lines | Where-Object { -not [string]::IsNullOrWhiteSpace($_) })) {
            $fullPath = Join-Path $RepositoryRoot $relativePath
            if (Test-Path -LiteralPath $fullPath -PathType Leaf) {
                $fileHash = (Get-FileHash -LiteralPath $fullPath -Algorithm SHA256).Hash.ToLowerInvariant()
                $parts.Add("untracked:${relativePath}:${fileHash}") | Out-Null
            }
        }
    }

    return Get-Sha256Text -Text ($parts -join "`n")
}

function Get-ExplicitDependencyNumbers {
    param([AllowEmptyString()][string]$IssueBody)

    if ([string]::IsNullOrWhiteSpace($IssueBody)) {
        return @()
    }

    $sectionMatch = [System.Text.RegularExpressions.Regex]::Match(
        $IssueBody,
        '(?ms)^##\s+Dependenc(?:y|ies)\s*$\s*(?<section>.*?)(?=^##\s+|\z)')

    if (-not $sectionMatch.Success) {
        return @()
    }

    $section = $sectionMatch.Groups["section"].Value
    $numberList = New-Object System.Collections.Generic.List[int]

    foreach ($rangeMatch in [System.Text.RegularExpressions.Regex]::Matches(
        $section,
        '#(?<start>\d+)\s*-\s*#?(?<end>\d+)\b')) {
        $start = [int]$rangeMatch.Groups["start"].Value
        $end = [int]$rangeMatch.Groups["end"].Value
        if ($end -ge $start -and ($end - $start) -le 100) {
            for ($number = $start; $number -le $end; $number++) {
                $numberList.Add($number) | Out-Null
            }
        }
    }

    foreach ($numberMatch in [System.Text.RegularExpressions.Regex]::Matches(
        $section,
        '#(?<number>\d+)\b')) {
        $numberList.Add([int]$numberMatch.Groups["number"].Value) | Out-Null
    }

    $numbers = @($numberList | Sort-Object -Unique)

    return $numbers
}

function Get-RepositoryNameFromRemote {
    param([AllowEmptyString()][string]$RemoteUrl)

    if ([string]::IsNullOrWhiteSpace($RemoteUrl)) {
        return $null
    }

    $match = [System.Text.RegularExpressions.Regex]::Match(
        $RemoteUrl,
        '(?i)(?:github\.com[/:])(?<owner>[^/]+)/(?<repo>[^/]+?)(?:\.git)?$')

    if (-not $match.Success) {
        return $null
    }

    return "$($match.Groups['owner'].Value)/$($match.Groups['repo'].Value)"
}

function Convert-CheckSummary {
    param([AllowNull()][object[]]$RawChecks)

    $checks = New-Object System.Collections.Generic.List[object]
    foreach ($rawCheck in @($RawChecks)) {
        if ($null -eq $rawCheck) {
            continue
        }

        $name = Get-OptionalProperty -InputObject $rawCheck -Name "name"
        if ([string]::IsNullOrWhiteSpace([string]$name)) {
            $name = Get-OptionalProperty -InputObject $rawCheck -Name "context" -DefaultValue "unnamed-check"
        }

        $result = Get-OptionalProperty -InputObject $rawCheck -Name "conclusion"
        if ([string]::IsNullOrWhiteSpace([string]$result)) {
            $result = Get-OptionalProperty -InputObject $rawCheck -Name "state"
        }
        if ([string]::IsNullOrWhiteSpace([string]$result)) {
            $result = Get-OptionalProperty -InputObject $rawCheck -Name "status" -DefaultValue "UNKNOWN"
        }

        $checks.Add([pscustomobject]@{
            Name = [string]$name
            Result = ([string]$result).ToUpperInvariant()
        }) | Out-Null
    }

    return $checks.ToArray()
}

$scriptPath = $MyInvocation.MyCommand.Path
$scriptDirectory = Split-Path -Parent $scriptPath
$repositoryRoot = [System.IO.Path]::GetFullPath((Join-Path $scriptDirectory "..\.."))
$warnings = New-Object System.Collections.Generic.List[string]
$verificationFailed = $false

if ([string]::IsNullOrWhiteSpace($OutputDirectory)) {
    $resolvedOutputDirectory = Join-Path $repositoryRoot ".agent"
}
elseif ([System.IO.Path]::IsPathRooted($OutputDirectory)) {
    $resolvedOutputDirectory = [System.IO.Path]::GetFullPath($OutputDirectory)
}
else {
    $resolvedOutputDirectory = [System.IO.Path]::GetFullPath((Join-Path $repositoryRoot $OutputDirectory))
}

New-Item -ItemType Directory -Path $resolvedOutputDirectory -Force | Out-Null

Push-Location $repositoryRoot
try {
    Invoke-Git -ArgumentList @("rev-parse", "--is-inside-work-tree") | Out-Null

    $headSha = (Invoke-Git -ArgumentList @("rev-parse", "HEAD")).Text
    $branch = (Invoke-Git -ArgumentList @("branch", "--show-current")).Text
    if ([string]::IsNullOrWhiteSpace($branch)) {
        $branch = "(detached)"
    }

    $branchIssueNumber = $null
    $branchIssueMatch = [System.Text.RegularExpressions.Regex]::Match(
        $branch,
        '(?i)(?:^|/)issue[-/](?<number>\d+)(?:[-/]|$)')
    if ($branchIssueMatch.Success) {
        $branchIssueNumber = [int]$branchIssueMatch.Groups["number"].Value
    }
    if ($IssueNumber -eq 0 -and $null -ne $branchIssueNumber) {
        $IssueNumber = $branchIssueNumber
    }

    $remoteUrlResult = Invoke-Git -ArgumentList @("remote", "get-url", $RemoteName) -AllowFailure
    $remoteUrl = if ($remoteUrlResult.ExitCode -eq 0) { $remoteUrlResult.Text } else { $null }

    $ghAvailable = $null -ne (Get-Command "gh" -ErrorAction SilentlyContinue)
    $repositoryMetadata = $null
    if (-not $Offline -and $ghAvailable) {
        $repositoryResult = Invoke-NativeCommand -FilePath "gh" -ArgumentList @(
            "repo", "view", "--json",
            "nameWithOwner,url,defaultBranchRef,isPrivate,visibility"
        ) -AllowFailure
        if ($repositoryResult.ExitCode -eq 0) {
            try {
                $repositoryMetadata = ConvertFrom-JsonSafe -Json $repositoryResult.Text -Description "gh repo view"
            }
            catch {
                $warnings.Add($_.Exception.Message) | Out-Null
            }
        }
        else {
            $warnings.Add("GitHub repository metadata was unavailable: $($repositoryResult.Text)") | Out-Null
        }
    }
    elseif (-not $Offline -and -not $ghAvailable) {
        $warnings.Add("GitHub CLI was not found; issue, pull request, and workflow state were skipped.") | Out-Null
    }

    $repositoryName = Get-OptionalProperty -InputObject $repositoryMetadata -Name "nameWithOwner"
    if ([string]::IsNullOrWhiteSpace([string]$repositoryName)) {
        $repositoryName = Get-RepositoryNameFromRemote -RemoteUrl $remoteUrl
    }

    $repositoryUrl = Get-OptionalProperty -InputObject $repositoryMetadata -Name "url"
    $visibility = Get-OptionalProperty -InputObject $repositoryMetadata -Name "visibility" -DefaultValue "UNKNOWN"
    $defaultBranchRef = Get-OptionalProperty -InputObject $repositoryMetadata -Name "defaultBranchRef"
    $defaultBranch = Get-OptionalProperty -InputObject $defaultBranchRef -Name "name"

    if ([string]::IsNullOrWhiteSpace([string]$defaultBranch)) {
        $symbolicRef = Invoke-Git -ArgumentList @(
            "symbolic-ref", "--quiet", "--short", "refs/remotes/$RemoteName/HEAD"
        ) -AllowFailure
        if ($symbolicRef.ExitCode -eq 0) {
            $defaultBranch = $symbolicRef.Text -replace "^$([regex]::Escape($RemoteName))/", ""
        }
        else {
            $defaultBranch = "main"
        }
    }

    $trackingRef = "refs/remotes/$RemoteName/$defaultBranch"
    $trackingResult = Invoke-Git -ArgumentList @("rev-parse", "--verify", $trackingRef) -AllowFailure
    $trackingDefaultSha = if ($trackingResult.ExitCode -eq 0) { $trackingResult.Text } else { $null }

    $remoteDefaultSha = $null
    if (-not $Offline -and -not [string]::IsNullOrWhiteSpace($remoteUrl)) {
        $remoteHeadResult = Invoke-Git -ArgumentList @(
            "ls-remote", "--heads", $RemoteName, "refs/heads/$defaultBranch"
        ) -AllowFailure
        if ($remoteHeadResult.ExitCode -eq 0 -and -not [string]::IsNullOrWhiteSpace($remoteHeadResult.Text)) {
            $remoteDefaultSha = ($remoteHeadResult.Text -split "\s+")[0]
        }
        elseif ($remoteHeadResult.ExitCode -ne 0) {
            $warnings.Add("Remote default-branch SHA was unavailable: $($remoteHeadResult.Text)") | Out-Null
        }
    }

    $mergeBaseSha = $headSha
    $ahead = 0
    $behind = 0
    if (-not [string]::IsNullOrWhiteSpace([string]$trackingDefaultSha)) {
        $mergeBaseResult = Invoke-Git -ArgumentList @("merge-base", "HEAD", $trackingRef) -AllowFailure
        if ($mergeBaseResult.ExitCode -eq 0) {
            $mergeBaseSha = $mergeBaseResult.Text
        }

        $countResult = Invoke-Git -ArgumentList @(
            "rev-list", "--left-right", "--count", "$trackingRef...HEAD"
        ) -AllowFailure
        if ($countResult.ExitCode -eq 0) {
            $counts = @($countResult.Text -split "\s+" | Where-Object { $_ -ne "" })
            if ($counts.Count -ge 2) {
                $behind = [int]$counts[0]
                $ahead = [int]$counts[1]
            }
        }
    }

    $statusResult = Invoke-Git -ArgumentList @("status", "--porcelain=v1")
    $worktreeChanges = @($statusResult.Lines | Where-Object { -not [string]::IsNullOrWhiteSpace($_) })
    $isDirty = $worktreeChanges.Count -gt 0
    $worktreeFingerprint = Get-WorktreeFingerprint -RepositoryRoot $repositoryRoot

    $changedFiles = @()
    if (-not [string]::IsNullOrWhiteSpace($mergeBaseSha)) {
        $changedResult = Invoke-Git -ArgumentList @(
            "diff", "--name-status", "$mergeBaseSha...$headSha"
        ) -AllowFailure
        if ($changedResult.ExitCode -eq 0) {
            $changedFiles = @($changedResult.Lines | Where-Object { -not [string]::IsNullOrWhiteSpace($_) })
        }
    }

    $staticContext = Get-StaticContextFingerprint -RepositoryRoot $repositoryRoot

    $issueSummary = $null
    $dependencyStates = @()
    $issueBodyFile = $null
    if ($IssueNumber -gt 0 -and -not $Offline -and $ghAvailable) {
        $issueResult = Invoke-NativeCommand -FilePath "gh" -ArgumentList @(
            "issue", "view", "$IssueNumber", "--json",
            "number,title,state,assignees,labels,milestone,updatedAt,url,body"
        ) -AllowFailure

        if ($issueResult.ExitCode -eq 0) {
            try {
                $rawIssue = ConvertFrom-JsonSafe -Json $issueResult.Text -Description "gh issue view"
                $issueBody = [string](Get-OptionalProperty -InputObject $rawIssue -Name "body" -DefaultValue "")
                $rawAssignees = @(Get-OptionalProperty -InputObject $rawIssue -Name "assignees" -DefaultValue @())
                $rawLabels = @(Get-OptionalProperty -InputObject $rawIssue -Name "labels" -DefaultValue @())
                $rawMilestone = Get-OptionalProperty -InputObject $rawIssue -Name "milestone"
                $dependencyNumbers = @(Get-ExplicitDependencyNumbers -IssueBody $issueBody)

                $issueSummary = [pscustomobject]@{
                    Number = [int](Get-OptionalProperty -InputObject $rawIssue -Name "number")
                    Title = [string](Get-OptionalProperty -InputObject $rawIssue -Name "title")
                    State = [string](Get-OptionalProperty -InputObject $rawIssue -Name "state")
                    UpdatedAt = ConvertTo-CanonicalTimestamp (Get-OptionalProperty -InputObject $rawIssue -Name "updatedAt")
                    Url = [string](Get-OptionalProperty -InputObject $rawIssue -Name "url")
                    Assignees = @($rawAssignees | ForEach-Object { Get-OptionalProperty -InputObject $_ -Name "login" })
                    Labels = @($rawLabels | ForEach-Object { Get-OptionalProperty -InputObject $_ -Name "name" })
                    Milestone = Get-OptionalProperty -InputObject $rawMilestone -Name "title"
                    BodyHash = Get-Sha256Text -Text $issueBody
                    Dependencies = $dependencyNumbers
                }

                $issueBodyFile = "issue-$IssueNumber.md"
                $issueDocument = @(
                    "# Issue #$IssueNumber - $($issueSummary.Title)",
                    "",
                    "Source: $($issueSummary.Url)",
                    "Updated: $($issueSummary.UpdatedAt)",
                    "Body SHA256: $($issueSummary.BodyHash)",
                    "",
                    $issueBody
                ) -join [Environment]::NewLine
                Write-Utf8File -Path (Join-Path $resolvedOutputDirectory $issueBodyFile) -Content $issueDocument

                $dependencyList = New-Object System.Collections.Generic.List[object]
                foreach ($dependencyNumber in $dependencyNumbers) {
                    $dependencyResult = Invoke-NativeCommand -FilePath "gh" -ArgumentList @(
                        "issue", "view", "$dependencyNumber", "--json",
                        "number,title,state,updatedAt,url"
                    ) -AllowFailure

                    if ($dependencyResult.ExitCode -eq 0) {
                        try {
                            $rawDependency = ConvertFrom-JsonSafe -Json $dependencyResult.Text -Description "dependency issue #$dependencyNumber"
                            $dependencyList.Add([pscustomobject]@{
                                Number = [int](Get-OptionalProperty -InputObject $rawDependency -Name "number")
                                Title = [string](Get-OptionalProperty -InputObject $rawDependency -Name "title")
                                State = [string](Get-OptionalProperty -InputObject $rawDependency -Name "state")
                                UpdatedAt = ConvertTo-CanonicalTimestamp (Get-OptionalProperty -InputObject $rawDependency -Name "updatedAt")
                                Url = [string](Get-OptionalProperty -InputObject $rawDependency -Name "url")
                            }) | Out-Null
                        }
                        catch {
                            $warnings.Add($_.Exception.Message) | Out-Null
                        }
                    }
                    else {
                        $dependencyList.Add([pscustomobject]@{
                            Number = $dependencyNumber
                            Title = $null
                            State = "UNKNOWN"
                            UpdatedAt = $null
                            Url = $null
                        }) | Out-Null
                        $warnings.Add("Dependency issue #$dependencyNumber was unavailable: $($dependencyResult.Text)") | Out-Null
                    }
                }
                $dependencyStates = $dependencyList.ToArray()
            }
            catch {
                $warnings.Add($_.Exception.Message) | Out-Null
            }
        }
        else {
            $warnings.Add("Issue #$IssueNumber was unavailable: $($issueResult.Text)") | Out-Null
        }
    }

    $pullRequestSummary = $null
    $pullRequestBodyFile = $null
    if (-not $Offline -and $ghAvailable -and $branch -ne "(detached)" -and $branch -ne $defaultBranch) {
        $pullRequestResult = Invoke-NativeCommand -FilePath "gh" -ArgumentList @(
            "pr", "view", "--json",
            "number,title,state,isDraft,headRefName,headRefOid,baseRefName,updatedAt,url,reviewDecision,statusCheckRollup,body"
        ) -AllowFailure

        if ($pullRequestResult.ExitCode -eq 0) {
            try {
                $rawPullRequest = ConvertFrom-JsonSafe -Json $pullRequestResult.Text -Description "gh pr view"
                $pullRequestBody = [string](Get-OptionalProperty -InputObject $rawPullRequest -Name "body" -DefaultValue "")
                $rawChecks = @(Get-OptionalProperty -InputObject $rawPullRequest -Name "statusCheckRollup" -DefaultValue @())
                $checks = @(Convert-CheckSummary -RawChecks $rawChecks)

                $pullRequestSummary = [pscustomobject]@{
                    Number = [int](Get-OptionalProperty -InputObject $rawPullRequest -Name "number")
                    Title = [string](Get-OptionalProperty -InputObject $rawPullRequest -Name "title")
                    State = [string](Get-OptionalProperty -InputObject $rawPullRequest -Name "state")
                    IsDraft = [bool](Get-OptionalProperty -InputObject $rawPullRequest -Name "isDraft" -DefaultValue $false)
                    HeadRefName = [string](Get-OptionalProperty -InputObject $rawPullRequest -Name "headRefName")
                    HeadRefOid = [string](Get-OptionalProperty -InputObject $rawPullRequest -Name "headRefOid")
                    BaseRefName = [string](Get-OptionalProperty -InputObject $rawPullRequest -Name "baseRefName")
                    UpdatedAt = ConvertTo-CanonicalTimestamp (Get-OptionalProperty -InputObject $rawPullRequest -Name "updatedAt")
                    Url = [string](Get-OptionalProperty -InputObject $rawPullRequest -Name "url")
                    ReviewDecision = [string](Get-OptionalProperty -InputObject $rawPullRequest -Name "reviewDecision" -DefaultValue "")
                    BodyHash = Get-Sha256Text -Text $pullRequestBody
                    Checks = $checks
                }

                $pullRequestBodyFile = "pr-$($pullRequestSummary.Number).md"
                $pullRequestDocument = @(
                    "# Pull request #$($pullRequestSummary.Number) - $($pullRequestSummary.Title)",
                    "",
                    "Source: $($pullRequestSummary.Url)",
                    "Updated: $($pullRequestSummary.UpdatedAt)",
                    "Body SHA256: $($pullRequestSummary.BodyHash)",
                    "",
                    $pullRequestBody
                ) -join [Environment]::NewLine
                Write-Utf8File -Path (Join-Path $resolvedOutputDirectory $pullRequestBodyFile) -Content $pullRequestDocument
            }
            catch {
                $warnings.Add($_.Exception.Message) | Out-Null
            }
        }
    }

    $latestRun = $null
    if (-not $Offline -and $ghAvailable -and $branch -ne "(detached)") {
        $runResult = Invoke-NativeCommand -FilePath "gh" -ArgumentList @(
            "run", "list", "--branch", $branch, "--limit", "1", "--json",
            "databaseId,workflowName,status,conclusion,headSha,createdAt,updatedAt,url"
        ) -AllowFailure
        if ($runResult.ExitCode -eq 0) {
            try {
                $parsedRuns = ConvertFrom-JsonSafe -Json $runResult.Text -Description "gh run list"
                $runs = @($parsedRuns | Where-Object { $null -ne $_ })
                if ($runs.Count -gt 0) {
                    $latestRun = $runs[0]
                }
            }
            catch {
                $warnings.Add($_.Exception.Message) | Out-Null
            }
        }
        elseif (-not [string]::IsNullOrWhiteSpace($runResult.Text)) {
            $warnings.Add("Workflow state was unavailable: $($runResult.Text)") | Out-Null
        }
    }

    $verificationPath = Join-Path $resolvedOutputDirectory "verification.json"
    $verificationRecord = $null
    if (Test-Path -LiteralPath $verificationPath) {
        try {
            $verificationRecord = ConvertFrom-JsonSafe -Json ([System.IO.File]::ReadAllText($verificationPath)) -Description "verification cache"
        }
        catch {
            $warnings.Add($_.Exception.Message) | Out-Null
        }
    }

    if ($Mode -eq "Verify") {
        $solution = Get-ChildItem -LiteralPath $repositoryRoot -File -Filter "*.sln" | Select-Object -First 1
        if ($null -eq $solution) {
            throw "No solution file was found in $repositoryRoot."
        }

        $testArguments = @(
            "test",
            $solution.Name,
            "--configuration", "Release",
            "--no-restore",
            "--verbosity", "minimal"
        )
        Write-Host "Running full Release verification for $headSha ..."
        $stopwatch = [System.Diagnostics.Stopwatch]::StartNew()
        $testResult = Invoke-NativeCommand -FilePath "dotnet" -ArgumentList $testArguments -AllowFailure
        $stopwatch.Stop()
        if (-not [string]::IsNullOrWhiteSpace($testResult.Text)) {
            Write-Host $testResult.Text
        }

        $verificationRecord = [pscustomobject]@{
            Sha = $headSha
            WorktreeFingerprint = $worktreeFingerprint
            WorktreeDirty = $isDirty
            Command = "dotnet $($testArguments -join ' ')"
            Outcome = if ($testResult.ExitCode -eq 0) { "SUCCESS" } else { "FAILURE" }
            ExitCode = $testResult.ExitCode
            DurationMilliseconds = $stopwatch.ElapsedMilliseconds
            CompletedAtUtc = [DateTime]::UtcNow.ToString("o")
        }
        Write-Utf8File -Path $verificationPath -Content ($verificationRecord | ConvertTo-Json -Depth 6)

        if ($testResult.ExitCode -ne 0) {
            $verificationFailed = $true
        }
    }

    $verificationCurrent = $false
    if ($null -ne $verificationRecord) {
        $verifiedSha = [string](Get-OptionalProperty -InputObject $verificationRecord -Name "Sha" -DefaultValue "")
        $verifiedWorktree = [string](Get-OptionalProperty -InputObject $verificationRecord -Name "WorktreeFingerprint" -DefaultValue "")
        $verificationOutcome = [string](Get-OptionalProperty -InputObject $verificationRecord -Name "Outcome" -DefaultValue "")
        $verificationCurrent = $verificationOutcome -eq "SUCCESS" -and
            $verifiedSha -eq $headSha -and
            $verifiedWorktree -eq $worktreeFingerprint
    }

    $remoteTrackingStale = -not [string]::IsNullOrWhiteSpace([string]$remoteDefaultSha) -and
        -not [string]::IsNullOrWhiteSpace([string]$trackingDefaultSha) -and
        $remoteDefaultSha -ne $trackingDefaultSha

    $openDependencies = @($dependencyStates | Where-Object { $_.State -eq "OPEN" })
    $failedChecks = @()
    $pendingChecks = @()
    if ($null -ne $pullRequestSummary) {
        $failedResults = @("FAILURE", "CANCELLED", "TIMED_OUT", "ACTION_REQUIRED", "STARTUP_FAILURE")
        $pendingResults = @("", "EXPECTED", "PENDING", "QUEUED", "IN_PROGRESS", "REQUESTED", "WAITING", "UNKNOWN")
        $failedChecks = @($pullRequestSummary.Checks | Where-Object { $_.Result -in $failedResults })
        $pendingChecks = @($pullRequestSummary.Checks | Where-Object { $_.Result -in $pendingResults })
    }

    $readiness = "READY"
    $nextAction = "Review the issue contract and begin the smallest relevant implementation/test step."

    if ($Offline) {
        $readiness = "REMOTE_STATE_UNKNOWN"
        $nextAction = "Refresh without -Offline before relying on issue, PR, dependency, or CI state."
    }
    elseif ($null -ne $issueSummary -and $issueSummary.State -ne "OPEN") {
        $readiness = "ISSUE_NOT_OPEN"
        $nextAction = "Stop implementation and confirm whether a new or reopened issue is required."
    }
    elseif ($openDependencies.Count -gt 0) {
        $readiness = "BLOCKED"
        $dependencyText = ($openDependencies | ForEach-Object { "#$($_.Number)" }) -join ", "
        $nextAction = "Wait for or help resolve open dependencies: $dependencyText."
    }
    elseif ($remoteTrackingStale) {
        $readiness = "LOCAL_REMOTE_STALE"
        $nextAction = "Fetch $RemoteName and refresh/rebase the issue branch before editing or verifying."
    }
    elseif ($IssueNumber -gt 0 -and $branch -ne $defaultBranch -and $branchIssueNumber -ne $IssueNumber) {
        $readiness = "BRANCH_TASK_MISMATCH"
        $nextAction = "Move to an issue/$IssueNumber-* branch created from the correct base before editing this task."
    }
    elseif ($branch -eq $defaultBranch -and $isDirty) {
        $readiness = "DIRTY_DEFAULT_BRANCH"
        $nextAction = "Preserve the current changes, then move the work to an isolated issue branch/worktree before continuing."
    }
    elseif ($branch -eq $defaultBranch -and $IssueNumber -gt 0) {
        $readiness = "NEEDS_ISSUE_BRANCH"
        $nextAction = "Create an isolated worktree and issue/$IssueNumber-* branch from the verified default branch."
    }
    elseif ($IssueNumber -gt 0 -and $null -eq $pullRequestSummary) {
        $readiness = "NEEDS_DRAFT_PR"
        $nextAction = "Open a draft PR for issue #$IssueNumber and record branch/base/current SHAs in its handoff."
    }
    elseif ($branch -ne $defaultBranch -and $null -eq $pullRequestSummary) {
        $readiness = "NEEDS_DRAFT_PR"
        $nextAction = "Open a draft PR and link the approved issue or human-requested governance task in its handoff."
    }
    elseif (-not $verificationCurrent) {
        $readiness = "VERIFICATION_STALE"
        $nextAction = "Run targeted tests, then rerun this script with -Mode Verify for the final worktree state."
    }
    elseif ($failedChecks.Count -gt 0) {
        $readiness = "CI_FAILED"
        $nextAction = "Inspect and fix the failing required checks before requesting review."
    }
    elseif ($pendingChecks.Count -gt 0) {
        $readiness = "CI_PENDING"
        $nextAction = "Wait for the current commit checks, then refresh once their state changes."
    }
    elseif ($null -ne $pullRequestSummary -and $pullRequestSummary.IsDraft) {
        $readiness = "IMPLEMENTATION_OR_REVIEW_PREP"
        $nextAction = "Complete acceptance evidence and handoff, then mark the draft PR ready for independent review."
    }
    elseif ($null -ne $pullRequestSummary) {
        $readiness = "READY_FOR_REVIEW"
        $nextAction = "Perform independent acceptance, architecture, scope, and test-sufficiency review."
    }

    $state = [ordered]@{
        SchemaVersion = "1.0"
        GeneratedAtUtc = [DateTime]::UtcNow.ToString("o")
        Mode = $Mode.ToUpperInvariant()
        Offline = [bool]$Offline
        Repository = [ordered]@{
            NameWithOwner = $repositoryName
            Url = $repositoryUrl
            Visibility = [string]$visibility
            Root = $repositoryRoot
            RemoteName = $RemoteName
            RemoteUrl = $remoteUrl
            DefaultBranch = [string]$defaultBranch
            Branch = $branch
            BranchIssueNumber = $branchIssueNumber
            HeadSha = $headSha
            MergeBaseSha = $mergeBaseSha
            TrackingDefaultSha = $trackingDefaultSha
            RemoteDefaultSha = $remoteDefaultSha
            RemoteTrackingStale = $remoteTrackingStale
            Ahead = $ahead
            Behind = $behind
            IsDirty = $isDirty
            WorktreeFingerprint = $worktreeFingerprint
            WorktreeChanges = $worktreeChanges
            ChangedFilesFromBase = $changedFiles
        }
        Task = [ordered]@{
            IssueNumber = if ($IssueNumber -gt 0) { $IssueNumber } else { $null }
            Issue = $issueSummary
            IssueBodyFile = $issueBodyFile
            Dependencies = $dependencyStates
        }
        PullRequest = $pullRequestSummary
        PullRequestBodyFile = $pullRequestBodyFile
        LatestWorkflowRun = $latestRun
        Verification = [ordered]@{
            IsCurrent = $verificationCurrent
            Record = $verificationRecord
        }
        StaticContext = [ordered]@{
            Hash = $staticContext.Hash
            Files = $staticContext.Files
        }
        Readiness = $readiness
        NextAction = $nextAction
        Warnings = $warnings.ToArray()
    }

    $statePath = Join-Path $resolvedOutputDirectory "state.json"
    Write-Utf8File -Path $statePath -Content ($state | ConvertTo-Json -Depth 12)

    $markdown = New-Object System.Collections.Generic.List[string]
    $markdown.Add("# Agent Context") | Out-Null
    $markdown.Add("") | Out-Null
    $markdown.Add("Generated UTC: $($state.GeneratedAtUtc)") | Out-Null
    $markdown.Add("Readiness: $readiness") | Out-Null
    $markdown.Add("Next action: $nextAction") | Out-Null
    $markdown.Add("") | Out-Null
    $markdown.Add("## Repository") | Out-Null
    $markdown.Add("") | Out-Null
    $markdown.Add("- Repository: $repositoryName") | Out-Null
    $markdown.Add("- Branch: $branch") | Out-Null
    $markdown.Add("- HEAD: $headSha") | Out-Null
    $markdown.Add("- Merge base: $mergeBaseSha") | Out-Null
    $markdown.Add("- Tracking $RemoteName/${defaultBranch}: $trackingDefaultSha") | Out-Null
    $markdown.Add("- Remote ${defaultBranch}: $remoteDefaultSha") | Out-Null
    $markdown.Add("- Ahead/behind tracking branch: $ahead/$behind") | Out-Null
    $markdown.Add("- Dirty: $isDirty") | Out-Null
    $markdown.Add("- Static context hash: $($staticContext.Hash)") | Out-Null

    if ($IssueNumber -gt 0) {
        $markdown.Add("") | Out-Null
        $markdown.Add("## Task") | Out-Null
        $markdown.Add("") | Out-Null
        if ($null -ne $issueSummary) {
            $markdown.Add("- Issue: #$IssueNumber $($issueSummary.Title)") | Out-Null
            $markdown.Add("- State / updated: $($issueSummary.State) / $($issueSummary.UpdatedAt)") | Out-Null
            $markdown.Add("- Assignees: $($issueSummary.Assignees -join ', ')") | Out-Null
            $markdown.Add("- Labels: $($issueSummary.Labels -join ', ')") | Out-Null
            $markdown.Add("- Body hash: $($issueSummary.BodyHash)") | Out-Null
            $markdown.Add("- Cached body: $issueBodyFile") | Out-Null
        }
        else {
            $markdown.Add("- Issue: #$IssueNumber (remote details unavailable)") | Out-Null
        }

        if ($dependencyStates.Count -gt 0) {
            $markdown.Add("- Dependencies:") | Out-Null
            foreach ($dependency in $dependencyStates) {
                $markdown.Add("  - #$($dependency.Number): $($dependency.State) - $($dependency.Title)") | Out-Null
            }
        }
        else {
            $markdown.Add("- Dependencies: none declared or unavailable") | Out-Null
        }
    }

    $markdown.Add("") | Out-Null
    $markdown.Add("## Pull request and CI") | Out-Null
    $markdown.Add("") | Out-Null
    if ($null -ne $pullRequestSummary) {
        $markdown.Add("- PR: #$($pullRequestSummary.Number) $($pullRequestSummary.Title)") | Out-Null
        $markdown.Add("- State / draft / review: $($pullRequestSummary.State) / $($pullRequestSummary.IsDraft) / $($pullRequestSummary.ReviewDecision)") | Out-Null
        $markdown.Add("- Updated: $($pullRequestSummary.UpdatedAt)") | Out-Null
        $markdown.Add("- Body hash / cache: $($pullRequestSummary.BodyHash) / $pullRequestBodyFile") | Out-Null
        if ($pullRequestSummary.Checks.Count -gt 0) {
            $markdown.Add("- Checks: $((@($pullRequestSummary.Checks | ForEach-Object { "$($_.Name)=$($_.Result)" })) -join ', ')") | Out-Null
        }
        else {
            $markdown.Add("- Checks: none reported") | Out-Null
        }
    }
    else {
        $markdown.Add("- PR: none found for the current branch") | Out-Null
    }

    if ($null -ne $latestRun) {
        $markdown.Add("- Latest run: $(Get-OptionalProperty -InputObject $latestRun -Name 'workflowName') / $(Get-OptionalProperty -InputObject $latestRun -Name 'status') / $(Get-OptionalProperty -InputObject $latestRun -Name 'conclusion') / $(Get-OptionalProperty -InputObject $latestRun -Name 'headSha')") | Out-Null
    }
    else {
        $markdown.Add("- Latest run: unavailable") | Out-Null
    }

    $markdown.Add("") | Out-Null
    $markdown.Add("## Verification") | Out-Null
    $markdown.Add("") | Out-Null
    if ($null -ne $verificationRecord) {
        $markdown.Add("- Current: $verificationCurrent") | Out-Null
        $markdown.Add("- Outcome / SHA: $(Get-OptionalProperty -InputObject $verificationRecord -Name 'Outcome') / $(Get-OptionalProperty -InputObject $verificationRecord -Name 'Sha')") | Out-Null
        $verificationCompletedAt = ConvertTo-CanonicalTimestamp (Get-OptionalProperty -InputObject $verificationRecord -Name "CompletedAtUtc")
        $markdown.Add("- Completed UTC: $verificationCompletedAt") | Out-Null
    }
    else {
        $markdown.Add("- No verification record. Run with -Mode Verify after targeted tests.") | Out-Null
    }

    if ($worktreeChanges.Count -gt 0 -or $changedFiles.Count -gt 0) {
        $markdown.Add("") | Out-Null
        $markdown.Add("## Changes") | Out-Null
        $markdown.Add("") | Out-Null
        foreach ($change in @($worktreeChanges | Select-Object -First 50)) {
            $markdown.Add("- Worktree: $change") | Out-Null
        }
        foreach ($change in @($changedFiles | Select-Object -First 50)) {
            $markdown.Add("- From base: $change") | Out-Null
        }
        if (($worktreeChanges.Count + $changedFiles.Count) -gt 100) {
            $markdown.Add("- Additional changes omitted from Markdown; inspect state.json.") | Out-Null
        }
    }

    if ($warnings.Count -gt 0) {
        $markdown.Add("") | Out-Null
        $markdown.Add("## Warnings") | Out-Null
        $markdown.Add("") | Out-Null
        foreach ($warning in $warnings) {
            $markdown.Add("- $warning") | Out-Null
        }
    }

    $contextPath = Join-Path $resolvedOutputDirectory "context.md"
    $markdownText = $markdown -join [Environment]::NewLine
    Write-Utf8File -Path $contextPath -Content $markdownText

    Write-Output $markdownText
    Write-Output ""
    Write-Output "Generated state: $statePath"
}
finally {
    Pop-Location
}

if ($verificationFailed) {
    exit 1
}
