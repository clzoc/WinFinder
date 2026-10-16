param(
    [string]$Configuration = "Release",
    [string]$Platform = "x64",
    [string]$DistRoot = "",
    [string]$EverythingSourceDir = "",
    [switch]$SkipBuild,
    [switch]$SkipEverythingRuntime,
    [switch]$NoZip,
    [switch]$IncludeSymbols,
    [switch]$IncludeXmlDocs
)

$ErrorActionPreference = "Stop"

function Resolve-MSBuild {
    $candidates = @(
        "${env:ProgramFiles}\Microsoft Visual Studio\18\Insiders\MSBuild\Current\Bin\amd64\MSBuild.exe",
        "${env:ProgramFiles}\Microsoft Visual Studio\2022\Community\MSBuild\Current\Bin\amd64\MSBuild.exe",
        "${env:ProgramFiles}\Microsoft Visual Studio\2022\Professional\MSBuild\Current\Bin\amd64\MSBuild.exe",
        "${env:ProgramFiles}\Microsoft Visual Studio\2022\Enterprise\MSBuild\Current\Bin\amd64\MSBuild.exe",
        "${env:ProgramFiles(x86)}\Microsoft Visual Studio\2019\Community\MSBuild\Current\Bin\MSBuild.exe"
    )

    foreach ($candidate in $candidates) {
        if ($candidate -and (Test-Path -LiteralPath $candidate)) {
            return $candidate
        }
    }

    $fromPath = Get-Command msbuild.exe -ErrorAction SilentlyContinue
    if ($fromPath) {
        return $fromPath.Source
    }

    throw "MSBuild.exe was not found. Install Visual Studio Build Tools or pass a prebuilt output with -SkipBuild."
}

function Invoke-MSBuildClean {
    param(
        [Parameter(Mandatory = $true)][string]$MSBuildPath,
        [Parameter(Mandatory = $true)][string[]]$Arguments
    )

    $startInfo = New-Object System.Diagnostics.ProcessStartInfo
    $startInfo.FileName = $MSBuildPath
    $startInfo.Arguments = ($Arguments | ForEach-Object { Quote-ProcessArgument $_ }) -join " "

    $startInfo.WorkingDirectory = $repoRoot
    $startInfo.UseShellExecute = $false
    $startInfo.CreateNoWindow = $true
    $startInfo.RedirectStandardOutput = $true
    $startInfo.RedirectStandardError = $true
    $startInfo.Environment.Clear()

    $machinePath = [Environment]::GetEnvironmentVariable("Path", "Machine")
    $userPath = [Environment]::GetEnvironmentVariable("Path", "User")
    $startInfo.Environment["Path"] = (($machinePath, $userPath) | Where-Object { -not [string]::IsNullOrWhiteSpace($_) }) -join ";"

    $envNames = @(
        "SystemRoot",
        "WINDIR",
        "TEMP",
        "TMP",
        "USERPROFILE",
        "LOCALAPPDATA",
        "APPDATA",
        "ProgramFiles",
        "ProgramFiles(x86)",
        "ProgramData",
        "ComSpec",
        "PROCESSOR_ARCHITECTURE",
        "PROCESSOR_IDENTIFIER",
        "NUMBER_OF_PROCESSORS"
    )

    foreach ($name in $envNames) {
        $value = [Environment]::GetEnvironmentVariable($name, "Process")
        if (-not [string]::IsNullOrWhiteSpace($value) -and -not $startInfo.Environment.ContainsKey($name)) {
            $startInfo.Environment[$name] = $value
        }
    }

    $process = [System.Diagnostics.Process]::Start($startInfo)
    $stdout = $process.StandardOutput.ReadToEnd()
    $stderr = $process.StandardError.ReadToEnd()
    $process.WaitForExit()

    if (-not [string]::IsNullOrWhiteSpace($stdout)) {
        Write-Host $stdout
    }

    if (-not [string]::IsNullOrWhiteSpace($stderr)) {
        Write-Error $stderr
    }

    if ($process.ExitCode -ne 0) {
        throw "MSBuild failed with exit code $($process.ExitCode)."
    }
}

function Quote-ProcessArgument {
    param([string]$Value)

    if ($null -eq $Value) {
        return '""'
    }

    if ($Value -notmatch '[\s"]') {
        return $Value
    }

    return '"' + $Value.Replace('\', '\\').Replace('"', '\"') + '"'
}

function Copy-RequiredFile {
    param(
        [Parameter(Mandatory = $true)][string]$Source,
        [Parameter(Mandatory = $true)][string]$Destination
    )

    if (-not (Test-Path -LiteralPath $Source -PathType Leaf)) {
        throw "Required file is missing: $Source"
    }

    $parent = Split-Path -Parent $Destination
    if ($parent) {
        New-Item -ItemType Directory -Force -Path $parent | Out-Null
    }

    Copy-Item -LiteralPath $Source -Destination $Destination -Force
}

function Copy-DirectoryClean {
    param(
        [Parameter(Mandatory = $true)][string]$Source,
        [Parameter(Mandatory = $true)][string]$Destination,
        [string[]]$ExcludeFileNames = @()
    )

    if (-not (Test-Path -LiteralPath $Source -PathType Container)) {
        throw "Required directory is missing: $Source"
    }

    if (Test-Path -LiteralPath $Destination) {
        Remove-Item -LiteralPath $Destination -Recurse -Force
    }

    New-Item -ItemType Directory -Force -Path $Destination | Out-Null
    Get-ChildItem -LiteralPath $Source -Force | ForEach-Object {
        if (-not $_.PSIsContainer -and ($ExcludeFileNames -contains $_.Name)) {
            return
        }

        Copy-Item -LiteralPath $_.FullName -Destination $Destination -Recurse -Force
    }
}

function Copy-RuntimeDirectory {
    param(
        [Parameter(Mandatory = $true)][string]$Source,
        [Parameter(Mandatory = $true)][string]$Destination,
        [string[]]$ExcludeFileNames = @()
    )

    if (-not (Test-Path -LiteralPath $Source -PathType Container)) {
        throw "Required runtime directory is missing: $Source"
    }

    New-Item -ItemType Directory -Force -Path $Destination | Out-Null

    $allowedExtensions = @(".exe", ".dll", ".config", ".json", ".md")
    if ($IncludeSymbols) {
        $allowedExtensions += ".pdb"
    }

    if ($IncludeXmlDocs) {
        $allowedExtensions += ".xml"
    }

    Get-ChildItem -LiteralPath $Source -File -Force | ForEach-Object {
        if ($ExcludeFileNames -contains $_.Name) {
            return
        }

        if ($allowedExtensions -contains $_.Extension.ToLowerInvariant()) {
            Copy-Item -LiteralPath $_.FullName -Destination (Join-Path $Destination $_.Name) -Force
        }
    }
}

function Assert-FileExists {
    param([Parameter(Mandatory = $true)][string]$Path)
    if (-not (Test-Path -LiteralPath $Path -PathType Leaf)) {
        throw "Packaged file is missing: $Path"
    }
}

function Assert-NoWorkerFfmpeg {
    param([Parameter(Mandatory = $true)][string]$ThumbnailerRoot)

    $blockedNames = @(
        "ffmpeg.exe",
        "avcodec-62.dll",
        "avdevice-62.dll",
        "avfilter-11.dll",
        "avformat-62.dll",
        "avutil-60.dll",
        "swresample-6.dll",
        "swscale-9.dll"
    )

    $workerFfmpegFiles = Get-ChildItem -LiteralPath (Join-Path $ThumbnailerRoot "Workers") -Recurse -File -ErrorAction SilentlyContinue |
        Where-Object { $blockedNames -contains $_.Name }

    if ($workerFfmpegFiles) {
        $paths = ($workerFfmpegFiles | Select-Object -ExpandProperty FullName) -join [Environment]::NewLine
        throw "FFmpeg binaries must be packaged only once under Thumbnailer\Tools\ffmpeg. Worker copies found:$([Environment]::NewLine)$paths"
    }
}

function Resolve-EverythingSource {
    if (-not [string]::IsNullOrWhiteSpace($EverythingSourceDir)) {
        if (Test-Path -LiteralPath (Join-Path $EverythingSourceDir "Everything.exe") -PathType Leaf) {
            return (Resolve-Path -LiteralPath $EverythingSourceDir).Path
        }

        throw "EverythingSourceDir does not contain Everything.exe: $EverythingSourceDir"
    }

    $candidates = @(
        (Join-Path $repoRoot "Tools\Everything"),
        (Join-Path $env:ProgramFiles "Everything"),
        (Join-Path ${env:ProgramFiles(x86)} "Everything")
    )

    foreach ($candidate in $candidates) {
        if ($candidate -and (Test-Path -LiteralPath (Join-Path $candidate "Everything.exe") -PathType Leaf)) {
            return (Resolve-Path -LiteralPath $candidate).Path
        }
    }

    return ""
}

function Copy-EverythingRuntime {
    param(
        [Parameter(Mandatory = $true)][string]$Source,
        [Parameter(Mandatory = $true)][string]$Destination
    )

    New-Item -ItemType Directory -Force -Path $Destination | Out-Null

    $files = @(
        "Everything.exe",
        "Everything.ini",
        "Everything.lng",
        "License.txt",
        "Changes.txt"
    )

    foreach ($file in $files) {
        $sourceFile = Join-Path $Source $file
        if (Test-Path -LiteralPath $sourceFile -PathType Leaf) {
            Copy-Item -LiteralPath $sourceFile -Destination (Join-Path $Destination $file) -Force
        }
    }

    Assert-FileExists (Join-Path $Destination "Everything.exe")
    Assert-FileExists (Join-Path $Destination "License.txt")
}

$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path
if ([string]::IsNullOrWhiteSpace($DistRoot)) {
    $DistRoot = Join-Path $repoRoot "dist"
}

$platformPath = if ($Platform -ieq "Any CPU") { "" } else { $Platform }
$buildOutput = if ([string]::IsNullOrWhiteSpace($platformPath)) {
    Join-Path $repoRoot "bin\$Configuration"
} else {
    Join-Path $repoRoot "bin\$platformPath\$Configuration"
}

$thumbnailerOutput = Join-Path $buildOutput "Thumbnailer"
$packageRoot = Join-Path $DistRoot "WinFinder"
$packageThumbnailer = Join-Path $packageRoot "Thumbnailer"
$packageZip = Join-Path $DistRoot "WinFinder-portable.zip"

if (-not $SkipBuild) {
    $msbuild = Resolve-MSBuild
    Write-Host "Building WinFinder.sln ($Configuration|$Platform)"
    Invoke-MSBuildClean -MSBuildPath $msbuild -Arguments @(
        (Join-Path $repoRoot "WinFinder.sln"),
        "/p:Configuration=$Configuration",
        "/p:Platform=$Platform",
        "/p:UseSharedCompilation=false",
        "/m:1",
        "/v:minimal"
    )
}

if (-not (Test-Path -LiteralPath $buildOutput -PathType Container)) {
    throw "Build output is missing: $buildOutput"
}

$resolvedDist = $ExecutionContext.SessionState.Path.GetUnresolvedProviderPathFromPSPath($DistRoot)
$resolvedRepo = $ExecutionContext.SessionState.Path.GetUnresolvedProviderPathFromPSPath($repoRoot)
if (-not $resolvedDist.StartsWith($resolvedRepo, [StringComparison]::OrdinalIgnoreCase)) {
    throw "Refusing to clean a dist path outside the repository: $resolvedDist"
}

Write-Host "Creating clean package: $packageRoot"
if (Test-Path -LiteralPath $packageRoot) {
    Remove-Item -LiteralPath $packageRoot -Recurse -Force
}
New-Item -ItemType Directory -Force -Path $packageRoot | Out-Null

Copy-RuntimeDirectory -Source $buildOutput -Destination $packageRoot
Copy-RequiredFile -Source (Join-Path $buildOutput "WinFinder.exe") -Destination (Join-Path $packageRoot "WinFinder.exe")
Copy-RequiredFile -Source (Join-Path $repoRoot "scripts\everything-service.ps1") -Destination (Join-Path $packageRoot "everything-service.ps1")

Copy-RuntimeDirectory -Source $thumbnailerOutput -Destination $packageThumbnailer
Copy-RequiredFile -Source (Join-Path $thumbnailerOutput "WinFinder.Thumbnailer.Service.exe") -Destination (Join-Path $packageThumbnailer "WinFinder.Thumbnailer.Service.exe")
Copy-RequiredFile -Source (Join-Path $thumbnailerOutput "pdfium.dll") -Destination (Join-Path $packageThumbnailer "pdfium.dll")

Copy-DirectoryClean -Source (Join-Path $thumbnailerOutput "Tools\ffmpeg") -Destination (Join-Path $packageThumbnailer "Tools\ffmpeg")
$ffmpegWorkerExcludes = @(
    "ffmpeg.exe",
    "avcodec-62.dll",
    "avdevice-62.dll",
    "avfilter-11.dll",
    "avformat-62.dll",
    "avutil-60.dll",
    "swresample-6.dll",
    "swscale-9.dll"
)
Copy-RuntimeDirectory -Source (Join-Path $thumbnailerOutput "Workers\Image") -Destination (Join-Path $packageThumbnailer "Workers\Image") -ExcludeFileNames $ffmpegWorkerExcludes
Copy-RuntimeDirectory -Source (Join-Path $thumbnailerOutput "Workers\Video") -Destination (Join-Path $packageThumbnailer "Workers\Video") -ExcludeFileNames $ffmpegWorkerExcludes

$everythingRuntimeSource = ""
if (-not $SkipEverythingRuntime) {
    $everythingRuntimeSource = Resolve-EverythingSource
    if ($everythingRuntimeSource) {
        Copy-EverythingRuntime -Source $everythingRuntimeSource -Destination (Join-Path $packageRoot "Everything")
    } else {
        throw "Everything.exe was not found. Place the Everything runtime under Tools\Everything or pass -EverythingSourceDir. Use -SkipEverythingRuntime only for intentionally incomplete diagnostic packages."
    }
}

Assert-FileExists (Join-Path $packageRoot "WinFinder.exe")
Assert-FileExists (Join-Path $packageRoot "Everything64.dll")
Assert-FileExists (Join-Path $packageRoot "pdfium.dll")
Assert-FileExists (Join-Path $packageThumbnailer "WinFinder.Thumbnailer.Service.exe")
Assert-FileExists (Join-Path $packageThumbnailer "pdfium.dll")
Assert-FileExists (Join-Path $packageThumbnailer "Tools\ffmpeg\ffmpeg.exe")
Assert-FileExists (Join-Path $packageThumbnailer "Workers\Image\WinFinder.Thumbnailer.Worker.Image.exe")
Assert-FileExists (Join-Path $packageThumbnailer "Workers\Video\WinFinder.Thumbnailer.Worker.Video.exe")
Assert-NoWorkerFfmpeg -ThumbnailerRoot $packageThumbnailer

$diagnoseExe = Join-Path $packageThumbnailer "WinFinder.Thumbnailer.Service.exe"
Write-Host "Running thumbnailer diagnostics from package"
$diagnoseOutput = & $diagnoseExe --diagnose
if ($LASTEXITCODE -ne 0) {
    throw "Thumbnailer diagnostics failed with exit code $LASTEXITCODE."
}

$diagnostics = $diagnoseOutput | ConvertFrom-Json
if (-not $diagnostics.dependencies.ffmpeg.isAvailable) {
    throw "Packaged thumbnailer cannot use bundled FFmpeg."
}

$imageFfmpeg = [string]$diagnostics.dependencies.ffmpeg.imageFfmpegPath
$videoFfmpeg = [string]$diagnostics.dependencies.ffmpeg.videoFfmpegPath
$sharedFfmpeg = Join-Path $packageThumbnailer "Tools\ffmpeg\ffmpeg.exe"
if ($imageFfmpeg -and (-not $imageFfmpeg.Equals($sharedFfmpeg, [StringComparison]::OrdinalIgnoreCase))) {
    throw "Image worker is not using the shared FFmpeg path. Reported: $imageFfmpeg"
}

if ($videoFfmpeg -and (-not $videoFfmpeg.Equals($sharedFfmpeg, [StringComparison]::OrdinalIgnoreCase))) {
    throw "Video worker is not using the shared FFmpeg path. Reported: $videoFfmpeg"
}

$manifest = [ordered]@{
    name = "WinFinder"
    configuration = $Configuration
    platform = $Platform
    generatedUtc = (Get-Date).ToUniversalTime().ToString("O")
    entryPoint = "WinFinder.exe"
    dependencies = [ordered]@{
        dotNetFramework = "4.7.2"
        thumbnailerService = "Thumbnailer\WinFinder.Thumbnailer.Service.exe"
        ffmpeg = "Thumbnailer\Tools\ffmpeg\ffmpeg.exe"
        pdfium = "Thumbnailer\pdfium.dll"
        everythingSdk = "Everything64.dll"
        everythingRuntime = if (Test-Path -LiteralPath (Join-Path $packageRoot "Everything\Everything.exe") -PathType Leaf) { "Everything\Everything.exe" } else { $null }
        everythingServiceScript = "everything-service.ps1"
    }
    notes = @(
        "Everything index data is generated per user on first run and is not prepackaged.",
        "Windows Shell thumbnails are fallback-only; bundled decoders handle primary thumbnail generation."
    )
}

$manifestPath = Join-Path $packageRoot "package-manifest.json"
Set-Content -LiteralPath $manifestPath -Value ($manifest | ConvertTo-Json -Depth 6) -Encoding UTF8

if (-not $NoZip) {
    if (Test-Path -LiteralPath $packageZip) {
        Remove-Item -LiteralPath $packageZip -Force
    }

    Compress-Archive -LiteralPath $packageRoot -DestinationPath $packageZip -CompressionLevel Optimal
}

$summary = [ordered]@{
    packageRoot = $packageRoot
    portableZip = if ($NoZip) { $null } else { $packageZip }
    configuration = $Configuration
    platform = $Platform
    thumbnailerBaseDirectory = $diagnostics.baseDirectory
    ffmpegPath = $sharedFfmpeg
    everythingSdkBundled = Test-Path -LiteralPath (Join-Path $packageRoot "Everything64.dll") -PathType Leaf
    everythingServiceBundled = Test-Path -LiteralPath (Join-Path $packageRoot "Everything\Everything.exe") -PathType Leaf
    everythingRuntimeSource = $everythingRuntimeSource
}

Write-Host ($summary | ConvertTo-Json -Depth 4)
