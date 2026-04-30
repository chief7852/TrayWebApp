# Signs TrayWebApp release binaries with a trusted code signing certificate.
# This script does not create or provide a certificate. Microsoft Store EXE/MSI submissions require
# a CA-backed certificate or Microsoft Trusted Signing/Azure Artifact Signing.

param(
    [string]$SignTool = "",
    [string]$PfxPath = "",
    [string]$PfxPassword = "",
    [string]$CertificateSubject = "",
    [string]$TimestampUrl = "http://timestamp.digicert.com",
    [switch]$UpdatePagesPackage
)

$ErrorActionPreference = "Stop"

function Resolve-SignTool {
    param([string]$ExplicitPath)

    if (-not [string]::IsNullOrWhiteSpace($ExplicitPath)) {
        if (-not (Test-Path $ExplicitPath)) {
            throw "signtool.exe was not found at: $ExplicitPath"
        }
        return (Resolve-Path $ExplicitPath).Path
    }

    $candidate = Get-ChildItem "C:\Program Files (x86)\Windows Kits\10\bin" -Recurse -Filter signtool.exe -ErrorAction SilentlyContinue |
        Where-Object { $_.FullName -match "\\x64\\signtool.exe$" } |
        Sort-Object FullName -Descending |
        Select-Object -First 1

    if (-not $candidate) {
        throw "signtool.exe was not found. Install Windows SDK or pass -SignTool."
    }

    return $candidate.FullName
}

function Invoke-Sign {
    param(
        [string]$FilePath,
        [string]$ToolPath
    )

    if (-not (Test-Path $FilePath)) {
        throw "File to sign was not found: $FilePath"
    }

    if (-not [string]::IsNullOrWhiteSpace($PfxPath)) {
        if (-not (Test-Path $PfxPath)) {
            throw "PFX file was not found: $PfxPath"
        }

        & $ToolPath sign /fd SHA256 /td SHA256 /tr $TimestampUrl /f $PfxPath /p $PfxPassword $FilePath
    }
    elseif (-not [string]::IsNullOrWhiteSpace($CertificateSubject)) {
        & $ToolPath sign /fd SHA256 /td SHA256 /tr $TimestampUrl /n $CertificateSubject $FilePath
    }
    else {
        throw "Provide either -PfxPath or -CertificateSubject."
    }

    if ($LASTEXITCODE -ne 0) {
        throw "Signing failed for: $FilePath"
    }
}

$resolvedSignTool = Resolve-SignTool $SignTool

$files = @(
    "publish\win-x64-final\TrayWebApp.exe",
    "publish\TrayWebApp-Setup.exe"
)

foreach ($file in $files) {
    Invoke-Sign -FilePath $file -ToolPath $resolvedSignTool
}

foreach ($file in $files) {
    $signature = Get-AuthenticodeSignature $file
    if ($signature.Status -ne "Valid") {
        throw "Signature is not valid for $file. Status: $($signature.Status)"
    }

    & $resolvedSignTool verify /pa /v $file
    if ($LASTEXITCODE -ne 0) {
        throw "signtool verify failed for: $file"
    }
}

if ($UpdatePagesPackage) {
    Copy-Item "publish\TrayWebApp-Setup.exe" "docs\download\TrayWebApp-Setup.exe" -Force
    Write-Host "Updated docs\download\TrayWebApp-Setup.exe with the signed installer." -ForegroundColor Green
}

Write-Host "Release binaries are signed and verified." -ForegroundColor Green
