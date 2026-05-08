param(
    [string]$PackageIdentityName = "chief7852.TrayWebApp",
    [string]$Publisher = "CN=chief7852",
    [string]$PublisherDisplayName = "chief7852",
    [string]$Version = "1.0.1.0",
    [string]$Runtime = "win-x64",
    [string]$Configuration = "Release",
    [string]$OutputDir = "publish\msix",
    [switch]$SkipPublish,
    [switch]$NoUploadPackage
)

$ErrorActionPreference = "Stop"

function Find-Tool {
    param(
        [Parameter(Mandatory = $true)]
        [string]$ToolName
    )

    $command = Get-Command $ToolName -ErrorAction SilentlyContinue
    if ($command) {
        return $command.Source
    }

    $kitRoot = Join-Path ${env:ProgramFiles(x86)} "Windows Kits\10\bin"
    if (Test-Path $kitRoot) {
        $tool = Get-ChildItem $kitRoot -Recurse -Filter $ToolName -ErrorAction SilentlyContinue |
            Where-Object { $_.FullName -match "\\x64\\" } |
            Sort-Object FullName -Descending |
            Select-Object -First 1

        if ($tool) {
            return $tool.FullName
        }
    }

    throw "$ToolName not found. Install Windows SDK first: winget install --id Microsoft.WindowsSDK.10.0.18362"
}

function Convert-To-AppxVersion {
    param([string]$InputVersion)

    $parts = $InputVersion.Split(".")
    if ($parts.Count -lt 4) {
        $parts = @($parts + @("0", "0", "0", "0"))[0..3]
    }

    return ($parts[0..3] -join ".")
}

function Assert-ManifestParameter {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Name,
        [Parameter(Mandatory = $true)]
        [string]$Value
    )

    if ([string]::IsNullOrWhiteSpace($Value)) {
        throw "$Name is required."
    }

    if ($Value -match "[<>]" -or $Value.Contains("Partner Center")) {
        throw "$Name has a placeholder value: '$Value'. Replace the sample text with the exact value from Partner Center. Do not include < or >."
    }

    if ($Name -eq "PackageIdentityName" -and $Value -match "\s") {
        throw "$Name must not contain spaces: '$Value'. Use the exact package identity name from Partner Center."
    }
}

function Escape-XmlValue {
    param([string]$Value)

    return [System.Security.SecurityElement]::Escape($Value)
}

function New-LogoAsset {
    param(
        [Parameter(Mandatory = $true)]
        [System.Drawing.Image]$Source,
        [Parameter(Mandatory = $true)]
        [string]$Path,
        [Parameter(Mandatory = $true)]
        [int]$Width,
        [Parameter(Mandatory = $true)]
        [int]$Height
    )

    $bitmap = New-Object System.Drawing.Bitmap($Width, $Height)
    $graphics = [System.Drawing.Graphics]::FromImage($bitmap)
    $graphics.Clear([System.Drawing.Color]::Transparent)
    $graphics.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
    $graphics.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::HighQuality
    $graphics.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality
    $graphics.CompositingQuality = [System.Drawing.Drawing2D.CompositingQuality]::HighQuality

    $scale = [Math]::Min($Width / $Source.Width, $Height / $Source.Height)
    $drawWidth = [int]($Source.Width * $scale)
    $drawHeight = [int]($Source.Height * $scale)
    $x = [int](($Width - $drawWidth) / 2)
    $y = [int](($Height - $drawHeight) / 2)

    $graphics.DrawImage($Source, $x, $y, $drawWidth, $drawHeight)
    $bitmap.Save($Path, [System.Drawing.Imaging.ImageFormat]::Png)

    $graphics.Dispose()
    $bitmap.Dispose()
}

$repoRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
Set-Location $repoRoot

Assert-ManifestParameter -Name "PackageIdentityName" -Value $PackageIdentityName
Assert-ManifestParameter -Name "Publisher" -Value $Publisher
Assert-ManifestParameter -Name "PublisherDisplayName" -Value $PublisherDisplayName

$appxVersion = Convert-To-AppxVersion $Version
$makeAppx = Find-Tool "makeappx.exe"
$projectPath = Join-Path $repoRoot "src\TrayWebApp.App\TrayWebApp.App.csproj"
$manifestTemplatePath = Join-Path $repoRoot "msix\Package.appxmanifest.template"
$sourceLogoPath = Join-Path $repoRoot "store-assets\logos\traywebapp-store-logo-1080.png"
$outputRoot = Join-Path $repoRoot $OutputDir
$publishDir = Join-Path $outputRoot "app"
$stageDir = Join-Path $outputRoot "staging"
$assetsDir = Join-Path $stageDir "Assets"
$packageName = "TrayWebApp_$appxVersion" + "_x64.msix"
$packagePath = Join-Path $outputRoot $packageName
$uploadName = "TrayWebApp_$appxVersion" + "_x64.msixupload"
$uploadPath = Join-Path $outputRoot $uploadName

if (-not (Test-Path $manifestTemplatePath)) {
    throw "Manifest template not found: $manifestTemplatePath"
}

if (-not (Test-Path $sourceLogoPath)) {
    throw "Source logo not found: $sourceLogoPath"
}

New-Item -ItemType Directory -Path $outputRoot -Force | Out-Null

if (-not $SkipPublish) {
    Write-Host "Publishing TrayWebApp for $Runtime..."
    dotnet publish $projectPath `
        -c $Configuration `
        -r $Runtime `
        -o $publishDir `
        --self-contained true `
        -p:PublishSingleFile=false `
        -p:PublishTrimmed=false `
        -p:DebugType=None `
        -p:DebugSymbols=false

    if ($LASTEXITCODE -ne 0) {
        throw "dotnet publish failed with exit code $LASTEXITCODE."
    }
}

if (-not (Test-Path (Join-Path $publishDir "TrayWebApp.exe"))) {
    throw "Published executable not found. Re-run without -SkipPublish."
}

if (Test-Path $stageDir) {
    Remove-Item $stageDir -Recurse -Force
}

New-Item -ItemType Directory -Path $stageDir -Force | Out-Null
Copy-Item (Join-Path $publishDir "*") $stageDir -Recurse -Force
New-Item -ItemType Directory -Path $assetsDir -Force | Out-Null

$manifest = Get-Content $manifestTemplatePath -Raw
$manifest = $manifest.Replace("__PACKAGE_IDENTITY_NAME__", (Escape-XmlValue $PackageIdentityName))
$manifest = $manifest.Replace("__PUBLISHER__", (Escape-XmlValue $Publisher))
$manifest = $manifest.Replace("__PUBLISHER_DISPLAY_NAME__", (Escape-XmlValue $PublisherDisplayName))
$manifest = $manifest.Replace("__VERSION__", $appxVersion)
$manifestPath = Join-Path $stageDir "AppxManifest.xml"
Set-Content -Path $manifestPath -Value $manifest -Encoding UTF8

Add-Type -AssemblyName System.Drawing
$sourceLogo = [System.Drawing.Image]::FromFile($sourceLogoPath)
try {
    New-LogoAsset -Source $sourceLogo -Path (Join-Path $assetsDir "StoreLogo.png") -Width 50 -Height 50
    New-LogoAsset -Source $sourceLogo -Path (Join-Path $assetsDir "Square44x44Logo.png") -Width 44 -Height 44
    New-LogoAsset -Source $sourceLogo -Path (Join-Path $assetsDir "Square71x71Logo.png") -Width 71 -Height 71
    New-LogoAsset -Source $sourceLogo -Path (Join-Path $assetsDir "Square150x150Logo.png") -Width 150 -Height 150
    New-LogoAsset -Source $sourceLogo -Path (Join-Path $assetsDir "Square310x310Logo.png") -Width 310 -Height 310
    New-LogoAsset -Source $sourceLogo -Path (Join-Path $assetsDir "Wide310x150Logo.png") -Width 310 -Height 150
}
finally {
    $sourceLogo.Dispose()
}

if (Test-Path $packagePath) {
    Remove-Item $packagePath -Force
}

Write-Host "Packing MSIX with MakeAppx..."
& $makeAppx pack /d $stageDir /p $packagePath /o
if ($LASTEXITCODE -ne 0) {
    throw "MakeAppx failed with exit code $LASTEXITCODE. Check $manifestPath for manifest values."
}

if (-not $NoUploadPackage) {
    $uploadStageDir = Join-Path $outputRoot "upload"
    $uploadZipPath = Join-Path $outputRoot "$uploadName.zip"

    if (Test-Path $uploadStageDir) {
        Remove-Item $uploadStageDir -Recurse -Force
    }
    if (Test-Path $uploadPath) {
        Remove-Item $uploadPath -Force
    }
    if (Test-Path $uploadZipPath) {
        Remove-Item $uploadZipPath -Force
    }

    New-Item -ItemType Directory -Path $uploadStageDir -Force | Out-Null
    Copy-Item $packagePath (Join-Path $uploadStageDir $packageName) -Force
    Compress-Archive -Path (Join-Path $uploadStageDir "*") -DestinationPath $uploadZipPath -Force
    Rename-Item $uploadZipPath $uploadName -Force
}

Write-Host ""
Write-Host "MSIX package created:"
Write-Host $packagePath
if (-not $NoUploadPackage) {
    Write-Host ""
    Write-Host "Store upload package created:"
    Write-Host $uploadPath
}
Write-Host ""
Write-Host "For Microsoft Store submission, rebuild with the exact PackageIdentityName and Publisher from Partner Center."
