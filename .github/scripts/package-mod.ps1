param(
    [Parameter(Mandatory = $true)]
    [ValidateSet("DisableSkillsBar", "HandyPurse", "PerfectDodge")]
    [string]$ModName
)

$ErrorActionPreference = "Stop"

$repoRoot = Resolve-Path (Join-Path $PSScriptRoot "../..")
Set-Location $repoRoot
$buildScript = Join-Path $repoRoot "build.bat"

$modDir = Join-Path $repoRoot $ModName
$csprojPath = Join-Path $modDir "$ModName.csproj"
$modSourcePath = Join-Path $modDir "${ModName}Mod.cs"
$dllPath = Join-Path $modDir "bin/Release/$ModName.dll"
$localizationDir = Join-Path $modDir "bin/Release/Assets/Localization"

if (-not (Test-Path $csprojPath)) {
    throw "Expected project file was not found: $csprojPath"
}

if (-not (Test-Path $modSourcePath)) {
    throw "Expected mod source file was not found: $modSourcePath"
}
if (-not (Test-Path $buildScript)) {
    throw "Build script not found: $buildScript"
}

$distRoot = Join-Path $repoRoot "dist"
$stagingRoot = Join-Path $distRoot "$ModName-staging"
$pluginDir = Join-Path $stagingRoot "plugins/fantastic-jam-$ModName"

if (Test-Path $stagingRoot) {
    Remove-Item -Recurse -Force $stagingRoot
}

if (Test-Path $dllPath) {
    Remove-Item -Force $dllPath
}

$source = Get-Content -LiteralPath $modSourcePath -Raw
$versionMatch = [regex]::Match($source, 'Version\s*=\s*"([^"]+)"')
if (-not $versionMatch.Success) {
    throw "Could not find Version constant in $modSourcePath"
}

$Version = $versionMatch.Groups[1].Value

Write-Host "Building $ModName in Release with current version '$Version'..."
& $buildScript
if ($LASTEXITCODE -ne 0) {
    throw "MSBuild failed for $ModName."
}

if (-not (Test-Path $dllPath)) {
    throw "Expected build output was not found: $dllPath"
}

New-Item -ItemType Directory -Force -Path $pluginDir | Out-Null
Copy-Item -LiteralPath $dllPath -Destination (Join-Path $pluginDir "$ModName.dll") -Force

if (Test-Path $localizationDir) {
    $localeFiles = Get-ChildItem -Path $localizationDir -Filter "*.json" -File
    if ($localeFiles.Count -gt 0) {
        $localizationOutDir = Join-Path $pluginDir "Assets/Localization"
        New-Item -ItemType Directory -Force -Path $localizationOutDir | Out-Null
        foreach ($localeFile in $localeFiles) {
            Copy-Item -LiteralPath $localeFile.FullName -Destination (Join-Path $localizationOutDir $localeFile.Name) -Force
        }
    }
}


$readmePath = Join-Path $modDir "README.md"
if (Test-Path $readmePath) {
    Copy-Item -LiteralPath $readmePath -Destination (Join-Path $pluginDir "README.md") -Force
}

if (-not (Test-Path $distRoot)) {
    New-Item -ItemType Directory -Force -Path $distRoot | Out-Null
}

$assetName = "$ModName-$Version.zip"
$assetPath = Join-Path $distRoot $assetName

if (Test-Path $assetPath) {
    Remove-Item -Force $assetPath
}

Compress-Archive -Path (Join-Path $stagingRoot "*") -DestinationPath $assetPath -Force

if ($env:GITHUB_OUTPUT) {
    "asset_path=$assetPath" | Out-File -FilePath $env:GITHUB_OUTPUT -Encoding utf8 -Append
    "asset_name=$assetName" | Out-File -FilePath $env:GITHUB_OUTPUT -Encoding utf8 -Append
}

Write-Host "Created release asset: $assetPath"
