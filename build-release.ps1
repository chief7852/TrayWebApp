# TrayWebApp release builder
# Publishes a self-contained executable, creates a zip package, and optionally builds an Inno Setup installer.

param(
    [string]$Configuration = "Release",
    [string]$Runtime = "win-x64",
    [string]$OutputDir = "publish\win-x64-final",
    [string]$Version = "1.0.2",
    [switch]$SkipInstaller
)

$ErrorActionPreference = "Stop"
$Root = $PSScriptRoot

Set-Location $Root

function New-PortableZip {
    param(
        [Parameter(Mandatory = $true)]
        [string]$SourceDir,
        [Parameter(Mandatory = $true)]
        [string]$DestinationPath
    )

    $lastError = $null
    for ($attempt = 1; $attempt -le 5; $attempt++) {
        try {
            if (Test-Path $DestinationPath) {
                Remove-Item -LiteralPath $DestinationPath -Force
            }

            Compress-Archive -Path (Join-Path $SourceDir "*") -DestinationPath $DestinationPath -Force -ErrorAction Stop
            return
        }
        catch {
            $lastError = $_
            Start-Sleep -Milliseconds (500 * $attempt)
        }
    }

    throw $lastError
}

Write-Host ""
Write-Host "========================================" -ForegroundColor Cyan
Write-Host "      TrayWebApp Release Builder" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""

& "$Root\publish.ps1" `
    -Configuration $Configuration `
    -Runtime $Runtime `
    -OutputDir $OutputDir `
    -SelfContained `
    -SingleFile

$ResolvedOutputDir = (Resolve-Path $OutputDir).Path
$PublishRoot = Join-Path $Root "publish"
New-Item -ItemType Directory -Force -Path $PublishRoot | Out-Null

$Timestamp = Get-Date -Format "yyyyMMdd-HHmm"
$ZipPath = Join-Path $PublishRoot "TrayWebApp-$Version-$Runtime-$Timestamp.zip"

Write-Host ""
Write-Host "[Zip] Creating portable package..." -ForegroundColor Yellow
New-PortableZip -SourceDir $ResolvedOutputDir -DestinationPath $ZipPath
Write-Host "  Zip: $ZipPath" -ForegroundColor Green

if ($SkipInstaller) {
    Write-Host ""
    Write-Host "Installer build skipped." -ForegroundColor Yellow
    return
}

$InnoCandidates = @()
$InnoCommand = Get-Command "ISCC.exe" -ErrorAction SilentlyContinue
if ($InnoCommand) {
    $InnoCandidates += $InnoCommand.Source
}
if (${env:ProgramFiles(x86)}) {
    $InnoCandidates += Join-Path ${env:ProgramFiles(x86)} "Inno Setup 6\ISCC.exe"
}
if ($env:ProgramFiles) {
    $InnoCandidates += Join-Path $env:ProgramFiles "Inno Setup 6\ISCC.exe"
}
if ($env:LOCALAPPDATA) {
    $InnoCandidates += Join-Path $env:LOCALAPPDATA "Programs\Inno Setup 6\ISCC.exe"
}

$InnoCompiler = $InnoCandidates | Where-Object { $_ -and (Test-Path $_) } | Select-Object -First 1
if (-not $InnoCompiler) {
    Write-Host ""
    Write-Host "Inno Setup compiler was not found. Portable zip and exe are ready." -ForegroundColor Yellow
    Write-Host "Install Inno Setup 6 and rerun this script to create an installer." -ForegroundColor Yellow
    return
}

Write-Host ""
Write-Host "[Installer] Building setup file with Inno Setup..." -ForegroundColor Yellow
& $InnoCompiler `
    "/DPublishDir=$ResolvedOutputDir" `
    "/DAppVersion=$Version" `
    "$Root\installer\TrayWebApp.iss"

if ($LASTEXITCODE -ne 0) {
    throw "Inno Setup failed with exit code $LASTEXITCODE"
}

Write-Host ""
Write-Host "Release build complete." -ForegroundColor Green
