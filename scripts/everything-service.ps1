param(
    [ValidateSet("install", "uninstall", "start", "stop", "status")]
    [string]$Action = "install",
    [string]$EverythingExe = "",
    [switch]$NoElevate
)

$ErrorActionPreference = "Stop"

function Test-IsAdministrator {
    $identity = [Security.Principal.WindowsIdentity]::GetCurrent()
    $principal = New-Object Security.Principal.WindowsPrincipal($identity)
    return $principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
}

function Resolve-EverythingExe {
    if (-not [string]::IsNullOrWhiteSpace($EverythingExe)) {
        if (Test-Path -LiteralPath $EverythingExe -PathType Leaf) {
            return (Resolve-Path -LiteralPath $EverythingExe).Path
        }

        throw "Everything.exe not found: $EverythingExe"
    }

    $candidates = @(
        (Join-Path $PSScriptRoot "Everything\Everything.exe"),
        (Join-Path $PSScriptRoot "..\Everything\Everything.exe"),
        (Join-Path $PSScriptRoot "..\Tools\Everything\Everything.exe"),
        (Join-Path $PSScriptRoot "Tools\Everything\Everything.exe")
    )

    foreach ($candidate in $candidates) {
        if (Test-Path -LiteralPath $candidate -PathType Leaf) {
            return (Resolve-Path -LiteralPath $candidate).Path
        }
    }

    throw "Everything.exe was not found beside this script."
}

function Invoke-Everything {
    param(
        [Parameter(Mandatory = $true)][string]$Exe,
        [Parameter(Mandatory = $true)][string[]]$Arguments
    )

    $process = Start-Process -FilePath $Exe -ArgumentList $Arguments -Wait -PassThru -WindowStyle Hidden
    if ($process.ExitCode -ne 0) {
        throw "Everything.exe $($Arguments -join ' ') failed with exit code $($process.ExitCode)."
    }
}

$requiresAdmin = $Action -ne "status"
if ($requiresAdmin -and -not $NoElevate -and -not (Test-IsAdministrator)) {
    $scriptPath = $PSCommandPath
    if ([string]::IsNullOrWhiteSpace($scriptPath)) {
        $scriptPath = $MyInvocation.MyCommand.Path
    }

    $argumentList = @(
        "-NoProfile",
        "-ExecutionPolicy",
        "Bypass",
        "-File",
        "`"$scriptPath`"",
        "-Action",
        $Action
    )

    if (-not [string]::IsNullOrWhiteSpace($EverythingExe)) {
        $argumentList += @("-EverythingExe", "`"$EverythingExe`"")
    }

    Start-Process -FilePath "powershell.exe" -ArgumentList $argumentList -Verb RunAs -Wait
    exit $LASTEXITCODE
}

$exe = Resolve-EverythingExe

switch ($Action) {
    "install" {
        Invoke-Everything -Exe $exe -Arguments @("-install-service")
        Invoke-Everything -Exe $exe -Arguments @("-start-service")
        Write-Host "Everything service installed and started from $exe"
    }
    "uninstall" {
        Invoke-Everything -Exe $exe -Arguments @("-stop-service")
        Invoke-Everything -Exe $exe -Arguments @("-uninstall-service")
        Write-Host "Everything service stopped and uninstalled."
    }
    "start" {
        Invoke-Everything -Exe $exe -Arguments @("-start-service")
        Write-Host "Everything service started."
    }
    "stop" {
        Invoke-Everything -Exe $exe -Arguments @("-stop-service")
        Write-Host "Everything service stopped."
    }
    "status" {
        $service = Get-Service -Name "Everything" -ErrorAction SilentlyContinue
        if ($service) {
            Write-Host "Everything service status: $($service.Status)"
        } else {
            Write-Host "Everything service is not installed."
        }
    }
}
