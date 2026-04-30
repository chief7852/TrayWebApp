# TrayWebApp Publish Script
# Creates a self-contained Windows executable

param(
    [string]$Configuration = "Release",
    [string]$Runtime = "win-x64",
    [string]$OutputDir = "",
    [switch]$SelfContained = $true,
    [switch]$SingleFile = $true
)

$ErrorActionPreference = "Stop"
$ProjectPath = "src\TrayWebApp.App\TrayWebApp.App.csproj"
if ([string]::IsNullOrWhiteSpace($OutputDir)) {
    $OutputDir = "publish\$Runtime"
}
$DotNetCommand = Get-Command dotnet -ErrorAction SilentlyContinue
$DotNetExe = if ($DotNetCommand) { $DotNetCommand.Source } else { "C:\Program Files\dotnet\dotnet.exe" }

if (-not (Test-Path $DotNetExe)) {
    throw ".NET SDK was not found. Install .NET SDK 8 or add dotnet.exe to PATH."
}

Write-Host ""
Write-Host "========================================" -ForegroundColor Cyan
Write-Host "      TrayWebApp Publish Script" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""

# Prepare publish output
if (Test-Path $OutputDir) {
    $ResolvedOutputDir = (Resolve-Path $OutputDir).Path
    $RunningProcesses = @(Get-Process TrayWebApp.App -ErrorAction SilentlyContinue |
        Where-Object {
            $_.Path -and
            $_.Path.StartsWith($ResolvedOutputDir, [StringComparison]::OrdinalIgnoreCase)
        })

    if ($RunningProcesses) {
        $ProcessIds = ($RunningProcesses | ForEach-Object { $_.Id }) -join ", "
        throw "Close running TrayWebApp process(es) before publishing. Process id(s): $ProcessIds"
    }
}
Write-Host "[1/3] Preparing output directory..." -ForegroundColor Yellow
New-Item -ItemType Directory -Force -Path $OutputDir | Out-Null

# Build arguments
$publishArgs = @(
    "publish", $ProjectPath,
    "-c", $Configuration,
    "-r", $Runtime,
    "-o", $OutputDir,
    "--self-contained", $SelfContained.ToString().ToLower()
)

if ($SingleFile) {
    $publishArgs += "-p:PublishSingleFile=true"
}

$publishArgs += "-p:PublishTrimmed=false"
$publishArgs += "-p:IncludeNativeLibrariesForSelfExtract=true"
$publishArgs += "-p:DebugType=None"
$publishArgs += "-p:DebugSymbols=false"

Write-Host "[2/3] Publishing..." -ForegroundColor Yellow
Write-Host "  Configuration: $Configuration"
Write-Host "  Runtime:       $Runtime"
Write-Host "  Self-Contained: $SelfContained"
Write-Host "  Single File:   $SingleFile"
Write-Host ""

& $DotNetExe @publishArgs

if ($LASTEXITCODE -ne 0) {
    Write-Host ""
    Write-Host "Publish failed!" -ForegroundColor Red
    exit 1
}

$ResolvedOutputDir = (Resolve-Path $OutputDir).Path
$ResolvedRoot = (Resolve-Path ".").Path
if (-not $ResolvedOutputDir.StartsWith($ResolvedRoot, [StringComparison]::OrdinalIgnoreCase)) {
    throw "Refusing to clean publish symbols outside the project directory: $ResolvedOutputDir"
}

foreach ($pattern in @("*.pdb", "*.xml")) {
    Get-ChildItem -LiteralPath $ResolvedOutputDir -Filter $pattern -File -ErrorAction SilentlyContinue |
        ForEach-Object { Remove-Item -LiteralPath $_.FullName -Force }
}

# Show results
Write-Host ""
Write-Host "[3/3] Done!" -ForegroundColor Green
Write-Host ""

$exeFile = Get-ChildItem "$OutputDir\*.exe" | Select-Object -First 1
if ($exeFile) {
    $sizeMB = [math]::Round($exeFile.Length / 1MB, 1)
    Write-Host "  Output:  $($exeFile.FullName)" -ForegroundColor White
    Write-Host "  Size:    $sizeMB MB" -ForegroundColor White
    Write-Host ""
    Write-Host "  Run with: .\$OutputDir\$($exeFile.Name)" -ForegroundColor Cyan
}

Write-Host ""
