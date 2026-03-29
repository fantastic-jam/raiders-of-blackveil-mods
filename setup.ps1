param(
    [string]$GameRoot
)

$ErrorActionPreference = 'Stop'

$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$defaultGameRoot = 'C:\Program Files (x86)\Steam\steamapps\common\Raiders of Blackveil'
$bepInExBuildsUrl = 'https://builds.bepinex.dev/projects/bepinex_be'
$script:GameRootWasAutoDetected = $false

function Show-Help {
    Write-Output 'Usage:'
    Write-Output '  setup.bat [GameRootPath]'
    Write-Output ''
    Write-Output 'Example:'
    Write-Output '  setup.bat "D:\SteamLibrary\steamapps\common\Raiders of Blackveil"'
    Write-Output ''
    Write-Output 'If [GameRootPath] is omitted, setup attempts Steam auto-detection.'
    Write-Output 'If detection fails, setup prompts for a path (Enter uses default).'
    Write-Output ''
    Write-Output 'Optional:'
    Write-Output '  set ROB_SETUP_SKIP_INSTALL=1 && setup.bat'
    Write-Output ''
}

function Resolve-GameRoot {
    param([string]$ProvidedGameRoot)

    if ($ProvidedGameRoot) {
        return $ProvidedGameRoot
    }

    $steamRoots = New-Object System.Collections.Generic.List[string]

    $regCandidates = @(
        @{ Key = 'HKCU:\Software\Valve\Steam'; Value = 'SteamPath' },
        @{ Key = 'HKCU:\Software\Valve\Steam'; Value = 'InstallPath' },
        @{ Key = 'HKLM:\SOFTWARE\WOW6432Node\Valve\Steam'; Value = 'InstallPath' },
        @{ Key = 'HKLM:\SOFTWARE\Valve\Steam'; Value = 'InstallPath' }
    )

    foreach ($candidate in $regCandidates) {
        try {
            $props = Get-ItemProperty -Path $candidate.Key -ErrorAction Stop
            $value = $props.($candidate.Value)
            if ($value) {
                $steamRoots.Add([string]$value)
            }
        } catch {
            # Ignore missing keys
        }
    }

    if (${env:ProgramFiles(x86)}) {
        $steamRoots.Add((Join-Path ${env:ProgramFiles(x86)} 'Steam'))
    }
    if ($env:ProgramFiles) {
        $steamRoots.Add((Join-Path $env:ProgramFiles 'Steam'))
    }

    $seen = New-Object System.Collections.Generic.HashSet[string]([System.StringComparer]::OrdinalIgnoreCase)

    foreach ($steamRootRaw in $steamRoots) {
        if ([string]::IsNullOrWhiteSpace($steamRootRaw)) {
            continue
        }

        try {
            $steamRoot = [System.IO.Path]::GetFullPath($steamRootRaw)
        } catch {
            continue
        }

        if (-not $seen.Add($steamRoot)) {
            continue
        }

        $directPath = Join-Path $steamRoot 'steamapps\common\Raiders of Blackveil'
        if (Test-Path (Join-Path $directPath 'RoB.exe')) {
            $script:GameRootWasAutoDetected = $true
            return $directPath
        }

        $libraryVdf = Join-Path $steamRoot 'steamapps\libraryfolders.vdf'
        if (-not (Test-Path $libraryVdf)) {
            continue
        }

        foreach ($line in Get-Content -Path $libraryVdf -ErrorAction SilentlyContinue) {
            if ($line -match '"path"\s+"([^"]+)"') {
                $libRoot = $Matches[1] -replace '\\\\', '\\'
                $candidatePath = Join-Path $libRoot 'steamapps\common\Raiders of Blackveil'
                if (Test-Path (Join-Path $candidatePath 'RoB.exe')) {
                    $script:GameRootWasAutoDetected = $true
                    return $candidatePath
                }
            }
        }
    }

    Write-Output 'WARNING: Could not auto-detect game path from Steam registry/libraries.'
    Write-Output "Press Enter to use default: $defaultGameRoot"
    $inputPath = Read-Host 'Game path'
    if ([string]::IsNullOrWhiteSpace($inputPath)) {
        return $defaultGameRoot
    }

    return $inputPath
}

function Find-Winget {
    $winget = (Get-Command winget.exe -ErrorAction SilentlyContinue | Select-Object -First 1 -ExpandProperty Source)
    if (-not $winget) {
        throw 'ERROR: winget.exe is not available. Install App Installer from Microsoft Store, then rerun.'
    }

    & $winget --version *> $null
    if ($LASTEXITCODE -ne 0) {
        throw 'ERROR: winget exists but failed to run non-interactively.'
    }

    return $winget
}

function Install-WithWinget {
    param(
        [string]$WingetExe,
        [string]$PackageId,
        [string]$PackageName
    )

    Write-Output "Installing $PackageName via winget..."
    & $WingetExe install --id $PackageId --exact --source winget --accept-source-agreements --accept-package-agreements --disable-interactivity --silent
    if ($LASTEXITCODE -ne 0) {
        Write-Output "Trying winget upgrade for $PackageName..."
        & $WingetExe upgrade --id $PackageId --exact --source winget --accept-source-agreements --accept-package-agreements --disable-interactivity --silent
    }
    if ($LASTEXITCODE -ne 0) {
        $listOutput = & $WingetExe list --id $PackageId --exact 2>&1
        $listText = ($listOutput | Out-String)
        if ($listText -match [regex]::Escape($PackageId)) {
            Write-Output "$PackageName is already installed."
            return
        }

        throw "ERROR: Failed to install or upgrade $PackageName."
    }
}

function Find-MSBuild {
    $msbuild = $null

    if (${env:ProgramFiles(x86)}) {
        $vswhere = Join-Path ${env:ProgramFiles(x86)} 'Microsoft Visual Studio\Installer\vswhere.exe'
        if (Test-Path $vswhere) {
            $msbuild = & $vswhere -latest -products * -requires Microsoft.Component.MSBuild -find 'MSBuild\**\Bin\MSBuild.exe' | Select-Object -First 1
        }
    }

    if (-not $msbuild) {
        $fallbackA = 'C:\Program Files\Microsoft Visual Studio\2022\BuildTools\MSBuild\Current\Bin\MSBuild.exe'
        $fallbackB = 'C:\Windows\WinSxS\amd64_msbuild_b03f5f7f11d50a3a_4.0.15920.100_none_d2f55ca1d7992ef6\MSBuild.exe'
        if (Test-Path $fallbackA) {
            $msbuild = $fallbackA
        } elseif (Test-Path $fallbackB) {
            $msbuild = $fallbackB
        }
    }

    if (-not $msbuild) {
        throw 'ERROR: Could not locate MSBuild.exe after setup.'
    }
    return $msbuild
}

function Write-UserPaths {
    param([string]$ResolvedGameRoot)

    $templatePath = Join-Path $scriptDir 'UserPaths.props.template'
    if (-not (Test-Path $templatePath)) {
        throw 'ERROR: UserPaths.props.template not found in repo root.'
    }

    $userPropsPath = Join-Path $scriptDir 'UserPaths.props'
    $xml = @"
<Project xmlns="http://schemas.microsoft.com/developer/msbuild/2003">
  <PropertyGroup>
    <RaidersOfBlackveilRootPath>$ResolvedGameRoot</RaidersOfBlackveilRootPath>
  </PropertyGroup>
</Project>
"@

    Set-Content -Path $userPropsPath -Value $xml -Encoding UTF8
    Write-Output 'Wrote UserPaths.props with game path.'
}

function Install-BepInExIfMissing {
    param([string]$ResolvedGameRoot)

    $bepInExDll = Join-Path $ResolvedGameRoot 'BepInEx\core\BepInEx.dll'
    $bepInExCoreDll = Join-Path $ResolvedGameRoot 'BepInEx\core\BepInEx.Core.dll'

    $needInstall = $true
    if (Test-Path $bepInExCoreDll) {
        Write-Output 'BepInEx 6.x already present.'
        $needInstall = $false
    } elseif (Test-Path $bepInExDll) {
        try {
            $installedVersion = [System.Reflection.AssemblyName]::GetAssemblyName($bepInExDll).Version
            Write-Output "Detected older BepInEx (v$installedVersion). Upgrading to 6.0.0 BE."
        } catch {
            Write-Output 'Could not read installed BepInEx version. Reinstalling latest 6.0.0 BE.'
        }
    }

    if (-not $needInstall) {
        return
    }

    if (-not (Test-Path (Join-Path $ResolvedGameRoot 'RoB.exe'))) {
        Write-Output "WARNING: RoB.exe not found at $ResolvedGameRoot"
        Write-Output 'Skipping automatic BepInEx install because game path appears invalid.'
        return
    }

    Write-Output 'Resolving latest BepInEx 6.0.0 BE Unity.Mono-win-x64 artifact...'
    $buildPageHtml = (Invoke-WebRequest -UseBasicParsing -Uri $bepInExBuildsUrl).Content
    $artifactRegex = 'href="(/projects/bepinex_be/\d+/BepInEx-Unity\.Mono-win-x64-6\.0\.0-be\.[^"/]+\.zip)"'
    $artifactMatch = [regex]::Match($buildPageHtml, $artifactRegex)
    if (-not $artifactMatch.Success) {
        throw 'ERROR: Could not resolve latest BepInEx BE Unity.Mono-win-x64 artifact URL.'
    }

    $url = "https://builds.bepinex.dev$($artifactMatch.Groups[1].Value)"
    Write-Output "Downloading $url"
    $zip = Join-Path $env:TEMP 'bepinex_rob.zip'

    Invoke-WebRequest -UseBasicParsing -Uri $url -OutFile $zip
    Expand-Archive -Path $zip -DestinationPath $ResolvedGameRoot -Force
    Remove-Item $zip -Force

    Write-Output 'BepInEx 6.0.0 BE installed into game folder.'
}

function Copy-GameAssemblies {
    param([string]$ResolvedGameRoot)

    $managed = Join-Path $ResolvedGameRoot 'RoB_Data\Managed'
    $sourceDll = Join-Path $managed 'Assembly-CSharp.dll'
    if (-not (Test-Path $sourceDll)) {
        Write-Output "WARNING: $sourceDll not found. Skipping lib copy."
        return
    }

    $libDir = Join-Path $scriptDir 'lib'
    if (-not (Test-Path $libDir)) {
        New-Item -Path $libDir -ItemType Directory | Out-Null
    }

    Copy-Item -Path $sourceDll -Destination (Join-Path $libDir 'Assembly-CSharp.dll') -Force
    Write-Output 'Copied Assembly-CSharp.dll to lib\ from game.'
}

function Ensure-IlspyCmd {
    $ilspyCmd = Join-Path $env:USERPROFILE '.dotnet\tools\ilspycmd.exe'
    if (Test-Path $ilspyCmd) {
        return $ilspyCmd
    }

    Write-Output 'Installing ilspycmd (dotnet global tool)...'
    & dotnet tool install --global ilspycmd --verbosity minimal
    if ($LASTEXITCODE -ne 0) {
        throw 'ERROR: Failed to install ilspycmd dotnet tool.'
    }

    if (-not (Test-Path $ilspyCmd)) {
        throw 'ERROR: ilspycmd installed but executable was not found in ~/.dotnet/tools.'
    }

    return $ilspyCmd
}

function Decompile-AssemblyCSharpIfMissing {
    $assemblyPath = Join-Path $scriptDir 'lib\Assembly-CSharp.dll'
    if (-not (Test-Path $assemblyPath)) {
        Write-Output 'WARNING: lib\Assembly-CSharp.dll not found. Skipping decompile.'
        return
    }

    $gameSrcDir = Join-Path $scriptDir 'game-src'
    if (Test-Path $gameSrcDir) {
        Write-Output 'game-src already exists. Skipping decompile.'
        return
    }

    $ilspyCmd = Ensure-IlspyCmd
    New-Item -Path $gameSrcDir -ItemType Directory -Force | Out-Null

    Write-Output 'Decompiling lib\Assembly-CSharp.dll into game-src\ ...'
    & $ilspyCmd -p -o $gameSrcDir $assemblyPath
    if ($LASTEXITCODE -ne 0) {
        throw 'ERROR: ilspycmd failed while decompiling Assembly-CSharp.dll.'
    }

    Write-Output 'Decompiled game code into game-src\.'
}

function Print-PathHints {
    param([string]$MSBuildPath)

    Write-Output ''
    Write-Output 'PATH entries to add manually if commands are not found:'

    if ($MSBuildPath) {
        Write-Output ("  MSBuild.exe : " + (Split-Path -Parent $MSBuildPath) + '\\')
    }

    $dotnet = (Get-Command dotnet.exe -ErrorAction SilentlyContinue | Select-Object -First 1 -ExpandProperty Source)
    if ($dotnet) {
        Write-Output ("  dotnet.exe  : " + (Split-Path -Parent $dotnet) + '\\')
    } else {
        Write-Output '  dotnet.exe  : not found in current PATH'
    }

    Write-Output ''
    Write-Output 'Note: BepInEx is installed in the game folder, not the project folder.'
}

if ($args.Count -gt 0 -and ($args[0] -eq '--help' -or $args[0] -eq '-h')) {
    Show-Help
    exit 0
}

try {
    $resolvedGameRoot = Resolve-GameRoot -ProvidedGameRoot $GameRoot
    if ($script:GameRootWasAutoDetected) {
        Write-Output "Auto-detected game path: $resolvedGameRoot"
    }

    Write-Output '=== Raiders of Blackveil Modding Setup ==='
    Write-Output "Repo : $scriptDir\\"
    Write-Output "Game : $resolvedGameRoot"
    Write-Output ''

    $wingetExe = Find-Winget
    Write-Output "Using winget: $wingetExe"
    $msbuildExe = Find-MSBuild
    Write-Output "Using MSBuild: $msbuildExe"

    if ($env:ROB_SETUP_SKIP_INSTALL -eq '1') {
        Write-Output 'Skipping package installs because ROB_SETUP_SKIP_INSTALL=1'
    } else {
        Install-WithWinget -WingetExe $wingetExe -PackageId 'Microsoft.DotNet.SDK.9' -PackageName '.NET SDK 9'

        if ($msbuildExe) {
            Write-Output 'MSBuild already detected. Skipping Build Tools installation.'
        } else {
            Write-Output 'Installing Visual Studio Build Tools via winget...'
            & $wingetExe install --id Microsoft.VisualStudio.2022.BuildTools --exact --source winget --accept-source-agreements --accept-package-agreements --disable-interactivity --override '--quiet --wait --norestart --nocache --add Microsoft.VisualStudio.Workload.ManagedDesktopBuildTools --includeRecommended'
            if ($LASTEXITCODE -ne 0) {
                Write-Output 'Trying winget upgrade for Visual Studio Build Tools...'
                & $wingetExe upgrade --id Microsoft.VisualStudio.2022.BuildTools --exact --source winget --accept-source-agreements --accept-package-agreements --disable-interactivity --override '--quiet --wait --norestart --nocache --add Microsoft.VisualStudio.Workload.ManagedDesktopBuildTools --includeRecommended'
            }
            if ($LASTEXITCODE -ne 0) {
                throw 'ERROR: Failed to install or upgrade Visual Studio Build Tools.'
            }

            $msbuildExe = Find-MSBuild
        }
    }

    Write-UserPaths -ResolvedGameRoot $resolvedGameRoot
    Install-BepInExIfMissing -ResolvedGameRoot $resolvedGameRoot
    Copy-GameAssemblies -ResolvedGameRoot $resolvedGameRoot
    Decompile-AssemblyCSharpIfMissing
    Print-PathHints -MSBuildPath $msbuildExe

    Write-Output ''
    Write-Output 'Setup complete.'
    Write-Output 'Next:'
    Write-Output '  1) Add the printed paths to your PATH manually (if needed)'
    Write-Output '  2) Run: build.bat'
    exit 0
} catch {
    Write-Output $_.Exception.Message
    exit 1
}
