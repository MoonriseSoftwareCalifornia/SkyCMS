param(
    [string]$RepoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path,
    [string]$OutputDirectory = '',
    [string]$BaselinePath = '',
    [double]$HighConfidenceMethodThreshold = 0.75,
    [double]$HighConfidenceTokenThreshold = 0.82,
    [switch]$FailOnHighConfidenceDuplicates,
    [switch]$FailOnNameCollisions
)

$ErrorActionPreference = 'Stop'

if ([string]::IsNullOrWhiteSpace($OutputDirectory)) {
    $OutputDirectory = Join-Path $RepoRoot 'TestResults\DuplicateTestAudit'
}

if ([string]::IsNullOrWhiteSpace($BaselinePath)) {
    $BaselinePath = Join-Path $RepoRoot '.github\duplicate-test-audit-baseline.json'
}

$testRoots = @(
    'Tests'
)

$rootPreference = @{
    'Tests' = 0
    'Other' = 9
}

function Get-RootLabel {
    param(
        [string]$RelativePath
    )

    foreach ($root in $testRoots) {
        if ($RelativePath.StartsWith($root, [System.StringComparison]::OrdinalIgnoreCase)) {
            return $root
        }
    }

    return 'Other'
}

function Get-RootPreference {
    param(
        [string]$Root
    )

    if ($rootPreference.ContainsKey($Root)) {
        return [int]$rootPreference[$Root]
    }

    return 99
}

function Get-ClassName {
    param(
        [string]$Content
    )

    $classRegexResult = [regex]::Match($Content, '(?ms)^\s*public\s+class\s+([A-Za-z_][A-Za-z0-9_]*)')
    if ($classRegexResult.Success) {
        return $classRegexResult.Groups[1].Value
    }

    return ''
}

function Get-TestMethods {
    param(
        [string]$Content
    )

    $regexResults = [regex]::Matches(
        $Content,
        '(?ms)\[(?:TestMethod|DataTestMethod)\][^\S\r\n]*(?:\r?\n\s*\[[^\]]+\])*\s*(?:public|internal)\s+(?:new\s+)?(?:async\s+)?(?:Task(?:<[^>]+>)?|ValueTask(?:<[^>]+>)?|void)\s+([A-Za-z_][A-Za-z0-9_]*)\s*\('
    )

    $methodNames = New-Object System.Collections.Generic.List[string]
    foreach ($regexResult in $regexResults) {
        $methodNames.Add($regexResult.Groups[1].Value)
    }

    return $methodNames
}

function Get-NormalizedContent {
    param(
        [string]$Content
    )

    $normalized = [regex]::Replace($Content, '(?ms)/\*.*?\*/', ' ')
    $normalized = [regex]::Replace($normalized, '(?m)^\s*//.*$', ' ')
    $normalized = [regex]::Replace($normalized, '(?m)^\s*using\s+.*?;\s*$', ' ')
    $normalized = [regex]::Replace($normalized, '\basync\b', ' ')
    $normalized = [regex]::Replace($normalized, '\bawait\b', ' ')
    $normalized = [regex]::Replace($normalized, '\bnew\s+void\b', ' void ')
    $normalized = [regex]::Replace($normalized, '\s+', ' ')
    return $normalized.Trim().ToLowerInvariant()
}

function Get-TokenSet {
    param(
        [string]$NormalizedContent
    )

    $ignore = [System.Collections.Generic.HashSet[string]]::new([System.StringComparer]::OrdinalIgnoreCase)
    @(
        'public', 'private', 'protected', 'internal', 'class', 'namespace', 'void', 'task',
        'return', 'var', 'using', 'true', 'false', 'null', 'string', 'int', 'bool', 'async',
        'testclass', 'testmethod', 'datatestmethod', 'summary', 'remarks', 'arrange', 'act', 'assert'
    ) | ForEach-Object { [void]$ignore.Add($_) }

    $tokens = [regex]::Split($NormalizedContent, '[^a-z0-9_]+') |
        Where-Object { $_.Length -gt 2 -and -not $ignore.Contains($_) }

    $set = [System.Collections.Generic.HashSet[string]]::new([System.StringComparer]::OrdinalIgnoreCase)
    foreach ($token in $tokens) {
        [void]$set.Add($token)
    }

    return $set
}

function Get-JaccardSimilarity {
    param(
        [System.Collections.Generic.HashSet[string]]$Left,
        [System.Collections.Generic.HashSet[string]]$Right
    )

    if (($null -eq $Left -or $Left.Count -eq 0) -and ($null -eq $Right -or $Right.Count -eq 0)) {
        return 1.0
    }

    if ($null -eq $Left -or $null -eq $Right -or $Left.Count -eq 0 -or $Right.Count -eq 0) {
        return 0.0
    }

    $intersection = 0
    foreach ($token in $Left) {
        if ($Right.Contains($token)) {
            $intersection++
        }
    }

    $union = $Left.Count + $Right.Count - $intersection
    if ($union -le 0) {
        return 0.0
    }

    return [Math]::Round($intersection / $union, 4)
}

function Get-MethodStats {
    param(
        [string[]]$LeftMethods,
        [string[]]$RightMethods
    )

    $leftSet = [System.Collections.Generic.HashSet[string]]::new([System.StringComparer]::OrdinalIgnoreCase)
    $rightSet = [System.Collections.Generic.HashSet[string]]::new([System.StringComparer]::OrdinalIgnoreCase)

    foreach ($method in $LeftMethods) {
        [void]$leftSet.Add($method)
    }

    foreach ($method in $RightMethods) {
        [void]$rightSet.Add($method)
    }

    $shared = [System.Collections.Generic.List[string]]::new()
    foreach ($method in $leftSet) {
        if ($rightSet.Contains($method)) {
            $shared.Add($method)
        }
    }

    $leftOnly = $leftSet.Where({ -not $rightSet.Contains($_) })
    $rightOnly = $rightSet.Where({ -not $leftSet.Contains($_) })

    $unionCount = $leftSet.Count + $rightSet.Count - $shared.Count
    $score = if ($unionCount -eq 0) { 0.0 } else { [Math]::Round($shared.Count / $unionCount, 4) }

    return [PSCustomObject]@{
        Score = $score
        Shared = $shared | Sort-Object
        LeftOnly = $leftOnly | Sort-Object
        RightOnly = $rightOnly | Sort-Object
    }
}

function Get-Suggestion {
    param(
        [pscustomobject]$Candidate,
        [pscustomobject]$Left,
        [pscustomobject]$Right
    )

    if ($Candidate.ExactContentMatch) {
        return 'High-confidence duplicate. Keep one class, remove or fold the other into a single maintained test suite.'
    }

    if ($Candidate.SharedMethodCount -eq $Left.TestMethodCount -and $Candidate.SharedMethodCount -eq $Right.TestMethodCount) {
        return 'Same test surface with minor implementation drift. Review both classes side by side and keep the better-maintained version.'
    }

    if ($Candidate.SharedMethodCount -gt 0 -and ($Candidate.LeftOnlyCount -gt 0 -or $Candidate.RightOnlyCount -gt 0)) {
        return 'Partial overlap. Merge shared methods into one class and preserve only the unique behavior checks from each side.'
    }

    return 'Name collision only. Verify whether the classes truly protect different behavior before consolidating.'
}

function Get-ReviewLevel {
    param(
        [double]$MethodOverlapScore,
        [double]$TokenSimilarityScore,
        [int]$SharedMethodCount,
        [bool]$ExactContentMatch,
        [bool]$HighConfidenceDuplicate
    )

    if ($HighConfidenceDuplicate -or $ExactContentMatch) {
        return 'High'
    }

    if ($MethodOverlapScore -ge 0.60 -or ($TokenSimilarityScore -ge 0.78 -and $SharedMethodCount -ge 2)) {
        return 'Medium'
    }

    return 'Low'
}

function Get-CanonicalRecommendation {
    param(
        [pscustomobject]$Left,
        [pscustomobject]$Right
    )

    $leftScore = Get-RootPreference -Root $Left.Root
    $rightScore = Get-RootPreference -Root $Right.Root

    if ($leftScore -lt $rightScore) {
        return [PSCustomObject]@{
            KeepPath = $Left.RelativePath
            RemovePath = $Right.RelativePath
            Reason = "Preferred root order favors $($Left.Root) over $($Right.Root)."
        }
    }

    if ($rightScore -lt $leftScore) {
        return [PSCustomObject]@{
            KeepPath = $Right.RelativePath
            RemovePath = $Left.RelativePath
            Reason = "Preferred root order favors $($Right.Root) over $($Left.Root)."
        }
    }

    if ($Left.TestMethodCount -gt $Right.TestMethodCount) {
        return [PSCustomObject]@{
            KeepPath = $Left.RelativePath
            RemovePath = $Right.RelativePath
            Reason = 'The recommended canonical class retains more distinct test methods.'
        }
    }

    if ($Right.TestMethodCount -gt $Left.TestMethodCount) {
        return [PSCustomObject]@{
            KeepPath = $Right.RelativePath
            RemovePath = $Left.RelativePath
            Reason = 'The recommended canonical class retains more distinct test methods.'
        }
    }

    if ($Left.RelativePath.Length -le $Right.RelativePath.Length) {
        return [PSCustomObject]@{
            KeepPath = $Left.RelativePath
            RemovePath = $Right.RelativePath
            Reason = 'Paths are otherwise equal, so the shorter canonical location is preferred.'
        }
    }

    return [PSCustomObject]@{
        KeepPath = $Right.RelativePath
        RemovePath = $Left.RelativePath
        Reason = 'Paths are otherwise equal, so the shorter canonical location is preferred.'
    }
}

$baselinePairs = [System.Collections.Generic.HashSet[string]]::new([System.StringComparer]::OrdinalIgnoreCase)
if (Test-Path $BaselinePath) {
    $baseline = Get-Content -LiteralPath $BaselinePath -Raw -Encoding UTF8 | ConvertFrom-Json
    foreach ($pairKey in @($baseline.HighConfidencePairKeys)) {
        [void]$baselinePairs.Add($pairKey)
    }
}

New-Item -ItemType Directory -Path $OutputDirectory -Force | Out-Null

$allFiles = foreach ($root in $testRoots) {
    $path = Join-Path $RepoRoot $root
    if (Test-Path $path) {
        Get-ChildItem -Path $path -Recurse -Filter '*Tests.cs' -File
    }
}

$tests = foreach ($file in $allFiles | Sort-Object FullName -Unique) {
    $relativePath = [System.IO.Path]::GetRelativePath($RepoRoot, $file.FullName)
    $content = Get-Content -LiteralPath $file.FullName -Raw -Encoding UTF8
    $methods = @(Get-TestMethods -Content $content)
    $normalizedContent = Get-NormalizedContent -Content $content
    $bytes = [System.Text.Encoding]::UTF8.GetBytes($normalizedContent)
    $hash = [System.Convert]::ToHexString([System.Security.Cryptography.SHA256]::HashData($bytes))

    [PSCustomObject]@{
        FileName = $file.Name
        RelativePath = $relativePath.Replace('\', '/')
        Root = Get-RootLabel -RelativePath $relativePath
        ClassName = Get-ClassName -Content $content
        TestMethodNames = $methods | Sort-Object -Unique
        TestMethodCount = ($methods | Sort-Object -Unique).Count
        NormalizedHash = $hash
        TokenSet = Get-TokenSet -NormalizedContent $normalizedContent
    }
}

$testsByPath = @{}
foreach ($test in $tests) {
    $testsByPath[$test.RelativePath] = $test
}

$pairIndex = [System.Collections.Generic.HashSet[string]]::new([System.StringComparer]::OrdinalIgnoreCase)
$candidates = New-Object System.Collections.Generic.List[object]

$groups = @()
$groups += @($tests | Group-Object FileName | Where-Object { $_.Count -gt 1 })
$groups += @(
    $tests |
        Where-Object { -not [string]::IsNullOrWhiteSpace($_.ClassName) } |
        Group-Object ClassName |
        Where-Object { $_.Count -gt 1 }
)

foreach ($group in $groups) {
    $items = @($group.Group)
    for ($leftIndex = 0; $leftIndex -lt $items.Count; $leftIndex++) {
        for ($rightIndex = $leftIndex + 1; $rightIndex -lt $items.Count; $rightIndex++) {
            $left = $items[$leftIndex]
            $right = $items[$rightIndex]

            if ($left.RelativePath -eq $right.RelativePath) {
                continue
            }

            $pairKey = (@($left.RelativePath, $right.RelativePath) | Sort-Object) -join '|'
            if (-not $pairIndex.Add($pairKey)) {
                continue
            }

            $methodStats = Get-MethodStats -LeftMethods $left.TestMethodNames -RightMethods $right.TestMethodNames
            $tokenScore = Get-JaccardSimilarity -Left $left.TokenSet -Right $right.TokenSet
            $exactContentMatch = $left.NormalizedHash -eq $right.NormalizedHash
            $isHighConfidence = $exactContentMatch -or (
                $methodStats.Score -ge $HighConfidenceMethodThreshold -and
                $tokenScore -ge $HighConfidenceTokenThreshold
            )
            $isReviewCandidate = $exactContentMatch -or
                ($methodStats.Score -ge 0.35) -or
                ($methodStats.Shared.Count -ge 3) -or
                ($tokenScore -ge 0.70 -and $methodStats.Shared.Count -ge 1)
            $reviewLevel = Get-ReviewLevel `
                -MethodOverlapScore $methodStats.Score `
                -TokenSimilarityScore $tokenScore `
                -SharedMethodCount $methodStats.Shared.Count `
                -ExactContentMatch $exactContentMatch `
                -HighConfidenceDuplicate $isHighConfidence
            $canonical = Get-CanonicalRecommendation -Left $left -Right $right
            $isBaselinePair = $baselinePairs.Contains($pairKey)

            $candidates.Add([PSCustomObject]@{
                PairKey = $pairKey
                LeftPath = $left.RelativePath
                LeftRoot = $left.Root
                LeftClass = $left.ClassName
                LeftMethodCount = $left.TestMethodCount
                RightPath = $right.RelativePath
                RightRoot = $right.Root
                RightClass = $right.ClassName
                RightMethodCount = $right.TestMethodCount
                SharedMethodCount = $methodStats.Shared.Count
                MethodOverlapScore = $methodStats.Score
                TokenSimilarityScore = $tokenScore
                ExactContentMatch = $exactContentMatch
                HighConfidenceDuplicate = $isHighConfidence
                ReviewCandidate = $isReviewCandidate
                ReviewLevel = $reviewLevel
                LeftOnlyCount = @($methodStats.LeftOnly).Count
                RightOnlyCount = @($methodStats.RightOnly).Count
                SharedMethods = @($methodStats.Shared)
                LeftOnlyMethods = @($methodStats.LeftOnly)
                RightOnlyMethods = @($methodStats.RightOnly)
                KeepPath = $canonical.KeepPath
                RemovePath = $canonical.RemovePath
                KeepReason = $canonical.Reason
                IsBaselinePair = $isBaselinePair
                NewHighConfidenceDuplicate = $isHighConfidence -and -not $isBaselinePair
                Suggestion = ''
            })
        }
    }
}

$candidates = $candidates |
    Sort-Object -Property @{ Expression = 'NewHighConfidenceDuplicate'; Descending = $true },
        @{ Expression = 'HighConfidenceDuplicate'; Descending = $true },
        @{ Expression = 'ReviewCandidate'; Descending = $true },
        @{ Expression = { switch ($_.ReviewLevel) { 'High' { 3 } 'Medium' { 2 } default { 1 } } }; Descending = $true },
        @{ Expression = 'ExactContentMatch'; Descending = $true },
        @{ Expression = 'MethodOverlapScore'; Descending = $true },
        @{ Expression = 'TokenSimilarityScore'; Descending = $true },
        @{ Expression = 'SharedMethodCount'; Descending = $true }

foreach ($candidate in $candidates) {
    $left = $testsByPath[$candidate.LeftPath]
    $right = $testsByPath[$candidate.RightPath]
    $candidate.Suggestion = Get-Suggestion -Candidate $candidate -Left $left -Right $right
}

$summary = [PSCustomObject]@{
    GeneratedAtUtc = [DateTime]::UtcNow.ToString('o')
    RepoRoot = $RepoRoot
    OutputDirectory = $OutputDirectory
    BaselinePath = $BaselinePath
    BaselinePairCount = $baselinePairs.Count
    TotalTestFiles = $tests.Count
    DuplicateFileNameGroups = @($tests | Group-Object FileName | Where-Object { $_.Count -gt 1 }).Count
    DuplicateClassNameGroups = @($tests | Where-Object { -not [string]::IsNullOrWhiteSpace($_.ClassName) } | Group-Object ClassName | Where-Object { $_.Count -gt 1 }).Count
    TotalCandidatePairs = @($candidates).Count
    HighConfidencePairs = @($candidates | Where-Object { $_.HighConfidenceDuplicate }).Count
    ReviewCandidatePairs = @($candidates | Where-Object { $_.ReviewCandidate }).Count
    MediumReviewPairs = @($candidates | Where-Object { $_.ReviewLevel -eq 'Medium' }).Count
    LowReviewPairs = @($candidates | Where-Object { $_.ReviewLevel -eq 'Low' }).Count
    ExactMatchPairs = @($candidates | Where-Object { $_.ExactContentMatch }).Count
    NewHighConfidencePairs = @($candidates | Where-Object { $_.NewHighConfidenceDuplicate }).Count
}

$report = [PSCustomObject]@{
    Summary = $summary
    CandidatePairs = @($candidates)
    DuplicateFileNameGroups = @(
        $tests |
            Group-Object FileName |
            Where-Object { $_.Count -gt 1 } |
            Sort-Object Name |
            ForEach-Object {
                [PSCustomObject]@{
                    FileName = $_.Name
                    Paths = $_.Group.RelativePath
                }
            }
    )
}

$jsonPath = Join-Path $OutputDirectory 'duplicate-test-audit.json'
$markdownPath = Join-Path $OutputDirectory 'duplicate-test-audit.md'

$report | ConvertTo-Json -Depth 8 | Set-Content -Path $jsonPath -Encoding UTF8

$markdown = New-Object System.Collections.Generic.List[string]
$markdown.Add('# Duplicate Test Audit')
$markdown.Add('')
$markdown.Add("Generated: $($summary.GeneratedAtUtc)")
$markdown.Add('')
$markdown.Add('## Summary')
$markdown.Add('')
$markdown.Add("- Total test files scanned: $($summary.TotalTestFiles)")
$markdown.Add("- Duplicate file-name groups: $($summary.DuplicateFileNameGroups)")
$markdown.Add("- Duplicate class-name groups: $($summary.DuplicateClassNameGroups)")
$markdown.Add("- Candidate pairs reviewed: $($summary.TotalCandidatePairs)")
$markdown.Add("- High-confidence duplicate pairs: $($summary.HighConfidencePairs)")
$markdown.Add("- Review candidates (same-name pairs): $($summary.ReviewCandidatePairs)")
$markdown.Add("- Medium review candidates: $($summary.MediumReviewPairs)")
$markdown.Add("- Low review candidates: $($summary.LowReviewPairs)")
$markdown.Add("- Exact normalized-content matches: $($summary.ExactMatchPairs)")
$markdown.Add("- Baseline pairs loaded: $($summary.BaselinePairCount)")
$markdown.Add("- Net-new high-confidence pairs: $($summary.NewHighConfidencePairs)")
$markdown.Add('')
$markdown.Add('## Sky.Tests Same-Name Review Candidates')
$markdown.Add('')

$reviewCandidates = @($candidates | Where-Object { $_.ReviewCandidate } | Select-Object -First 25)
if ($reviewCandidates.Count -eq 0) {
    $markdown.Add('No additional review candidates were found for same-name pairs in Sky.Tests.')
}
else {
    foreach ($candidate in $reviewCandidates) {
        $markdown.Add("### [$($candidate.ReviewLevel)] $($candidate.LeftPath) <-> $($candidate.RightPath)")
        $markdown.Add('')
        $markdown.Add("- Method overlap: $($candidate.SharedMethodCount) shared, score $($candidate.MethodOverlapScore)")
        $markdown.Add("- Token similarity: $($candidate.TokenSimilarityScore)")
        $markdown.Add("- Recommended keep: $($candidate.KeepPath)")
        $markdown.Add("- Recommended remove: $($candidate.RemovePath)")
        $markdown.Add("- Suggested action: $($candidate.Suggestion)")
        $markdown.Add('')
    }
}

$markdown.Add('## Net-New High-Confidence Duplicates')
$markdown.Add('')

$newPairs = @($candidates | Where-Object { $_.NewHighConfidenceDuplicate } | Select-Object -First 25)
if ($newPairs.Count -eq 0) {
    $markdown.Add('No net-new high-confidence duplicate pairs were found relative to the baseline.')
}
else {
    foreach ($candidate in $newPairs) {
        $markdown.Add("### $($candidate.LeftPath) <-> $($candidate.RightPath)")
        $markdown.Add('')
        $markdown.Add("- Recommended keep: $($candidate.KeepPath)")
        $markdown.Add("- Recommended remove: $($candidate.RemovePath)")
        $markdown.Add("- Why: $($candidate.KeepReason)")
        $markdown.Add("- Suggested action: $($candidate.Suggestion)")
        $markdown.Add('')
    }
}

$markdown.Add('## High-Confidence Duplicate Candidates')
$markdown.Add('')

$highConfidencePairs = @($candidates | Where-Object { $_.HighConfidenceDuplicate } | Select-Object -First 25)
if ($highConfidencePairs.Count -eq 0) {
    $markdown.Add('No high-confidence duplicate candidates were found.')
}
else {
    foreach ($candidate in $highConfidencePairs) {
        $markdown.Add("### $($candidate.LeftPath) <-> $($candidate.RightPath)")
        $markdown.Add('')
        $markdown.Add("- Method overlap: $($candidate.SharedMethodCount) shared, score $($candidate.MethodOverlapScore)")
        $markdown.Add("- Token similarity: $($candidate.TokenSimilarityScore)")
        $markdown.Add("- Exact normalized match: $($candidate.ExactContentMatch)")
        $markdown.Add("- Left-only methods: $($candidate.LeftOnlyCount)")
        $markdown.Add("- Right-only methods: $($candidate.RightOnlyCount)")
        $markdown.Add("- Recommended keep: $($candidate.KeepPath)")
        $markdown.Add("- Recommended remove: $($candidate.RemovePath)")
        $markdown.Add("- Why: $($candidate.KeepReason)")
        $markdown.Add("- Suggested action: $($candidate.Suggestion)")
        if ($candidate.SharedMethods.Count -gt 0) {
            $markdown.Add("- Shared methods: $([string]::Join(', ', $candidate.SharedMethods))")
        }

        if ($candidate.LeftOnlyMethods.Count -gt 0) {
            $markdown.Add("- Left-only methods: $([string]::Join(', ', ($candidate.LeftOnlyMethods | Select-Object -First 10)))")
        }

        if ($candidate.RightOnlyMethods.Count -gt 0) {
            $markdown.Add("- Right-only methods: $([string]::Join(', ', ($candidate.RightOnlyMethods | Select-Object -First 10)))")
        }

        $markdown.Add('')
    }
}

$markdown.Add('## Duplicate File Names Across Trees')
$markdown.Add('')
foreach ($group in ($report.DuplicateFileNameGroups | Select-Object -First 50)) {
    $markdown.Add("### $($group.FileName)")
    foreach ($path in $group.Paths) {
        $markdown.Add("- $path")
    }

    $markdown.Add('')
}

$markdown | Set-Content -Path $markdownPath -Encoding UTF8

Write-Host "Duplicate test audit written to $markdownPath"
Write-Host "Machine-readable report written to $jsonPath"

if ($FailOnHighConfidenceDuplicates -and $summary.NewHighConfidencePairs -gt 0) {
    throw "Found $($summary.NewHighConfidencePairs) net-new high-confidence duplicate test pairs."
}

if ($FailOnNameCollisions -and ($summary.DuplicateFileNameGroups -gt 0 -or $summary.DuplicateClassNameGroups -gt 0)) {
    throw "Found duplicate test name collisions (files: $($summary.DuplicateFileNameGroups), classes: $($summary.DuplicateClassNameGroups))."
}
