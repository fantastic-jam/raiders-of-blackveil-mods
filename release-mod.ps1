param(
    [Parameter(Mandatory = $true)]
    [ValidateSet("DisableSkillsBar", "HandyPurse", "PerfectDodge")]
    [string]$ModName,

    [Parameter(Mandatory = $true)]
    [string]$Version,

    [switch]$SkipPush,
    [switch]$SkipRelease,
    [switch]$DryRun
)

$ErrorActionPreference = "Stop"

$repoRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
Set-Location $repoRoot

if ($Version -notmatch '^[0-9]+\.[0-9]+\.[0-9]+(?:[-+][0-9A-Za-z.-]+)?$') {
    throw "Invalid version '$Version'. Expected SemVer (example: 1.2.0 or 1.2.0-beta.1)."
}

$tag = "$ModName-v$Version"
$assetPath = Join-Path $repoRoot "dist/$ModName-$Version.zip"
$packageScript = Join-Path $repoRoot ".github/scripts/package-mod.ps1"
$sourcePath = Join-Path $repoRoot "$ModName/${ModName}Mod.cs"

$branchRaw = git rev-parse --abbrev-ref HEAD
if ($LASTEXITCODE -ne 0) {
    throw "Failed to determine current git branch."
}
$branch = (($branchRaw -join "`n")).Trim()
if ($branch -ne "main") {
    throw "Releases must be created from the main branch. Current branch: $branch"
}

$trackedChanges = (git status --porcelain)
if ($trackedChanges -and -not $DryRun) {
    throw "Working tree is not clean. Commit or stash changes before running release-mod."
}

if ($DryRun) {
    $SkipPush = $true
    $SkipRelease = $true
}

if ($SkipPush -and -not $SkipRelease) {
    throw "Cannot create a GitHub release when -SkipPush is set because the release tag would not exist on origin."
}

if ((-not $SkipRelease) -and -not (Get-Command gh -ErrorAction SilentlyContinue)) {
    throw "GitHub CLI 'gh' is not installed or not in PATH."
}
if (-not (Get-Command git -ErrorAction SilentlyContinue)) {
    throw "git is not installed or not in PATH."
}
if (-not (Test-Path $packageScript)) {
    throw "Package script not found: $packageScript"
}
if (-not (Test-Path $sourcePath)) {
    throw "Mod source file not found: $sourcePath"
}

# Find the most recent previous release tag for this mod (used for changelog range)
$prevTagRaw = git tag -l "$ModName-v*" --sort=-version:refname 2>$null | Select-Object -First 1
$prevTag = if ($prevTagRaw) { ($prevTagRaw -join "").Trim() } else { $null }

$localTagRaw = git tag -l "$tag"
if ($LASTEXITCODE -ne 0) {
    throw "Failed to check local tags."
}
$localTag = (($localTagRaw -join "`n")).Trim()
if ($localTag) {
    throw "Tag already exists locally: $tag"
}

$remoteTagRaw = git ls-remote --tags origin "refs/tags/$tag"
if ($LASTEXITCODE -ne 0) {
    throw "Failed to check remote tags from origin."
}
$remoteTag = (($remoteTagRaw -join "`n")).Trim()
if ($remoteTag) {
    throw "Tag already exists on origin: $tag"
}

$range = if ($prevTag) { "$prevTag..HEAD" } else { "HEAD" }
$commitLines = git log $range --pretty=format:"- %s" --no-merges --grep="$ModName" -i
$changelog = if ($commitLines) {
    "## Changelog`n`n" + ($commitLines -join "`n")
} else {
    "No changes recorded."
}

if ($DryRun) {
    $prevTagDisplay = if ($prevTag) { $prevTag } else { '(none - first release)' }
    $summary = @"

=== DRY RUN - nothing will be modified ===

  Mod:        $ModName
  Version:    $Version
  Tag:        $tag
  Branch:     $branch
  Prev tag:   $prevTagDisplay
  Asset:      $assetPath

  Steps that would run:
    1. Bump Version in $sourcePath
    2. Build and package -> $assetPath
    3. git commit -m "chore($ModName): release v$Version"
    4. git tag -a $tag
    5. git push origin $branch
    6. git push origin $tag
    7. gh release create $tag --title "$ModName v$Version"
    8. gh release upload $tag $assetPath

  Release notes:
$changelog

"@
    Write-Host $summary
    return
}

$source = Get-Content -LiteralPath $sourcePath -Raw
$versionTokenPattern = 'Version\s*=\s*"[^"]*"'
$updated = [regex]::Replace(
    $source,
    $versionTokenPattern,
    ('Version = "' + $Version + '"'),
    1
)
if ($updated -eq $source) {
    throw "Could not find Version constant to update in $sourcePath"
}

Set-Content -LiteralPath $sourcePath -Value $updated -Encoding utf8

try {
    Write-Host "Packaging $ModName $Version..."
    & $packageScript -ModName $ModName
    if ($LASTEXITCODE -ne 0) {
        throw "Packaging failed for $ModName $Version."
    }

    if (-not (Test-Path $assetPath)) {
        throw "Expected release asset was not produced: $assetPath"
    }
}
catch {
    Set-Content -LiteralPath $sourcePath -Value $source -Encoding utf8
    throw
}

git add "$ModName/${ModName}Mod.cs"
if (-not (git diff --cached --quiet)) {
    git commit -m "chore($ModName): release v$Version"
}

git tag -a "$tag" -m "$tag"

if (-not $SkipPush) {
    git push origin "$branch"
    git push origin "$tag"
}

if (-not $SkipRelease) {
    $notesFile = [System.IO.Path]::GetTempFileName()
    try {
        Set-Content -LiteralPath $notesFile -Value $changelog -Encoding utf8
        gh release create "$tag" --title "$ModName v$Version" --notes-file $notesFile
        gh release upload "$tag" "$assetPath" --clobber
    } finally {
        Remove-Item $notesFile -ErrorAction SilentlyContinue
    }
}

Write-Host "Release completed for $tag"
