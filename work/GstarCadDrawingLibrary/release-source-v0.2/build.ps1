param(
    [string]$Configuration = "Release",
    [string]$GstarCadInstallDir = ""
)

$ErrorActionPreference = "Stop"
$projectDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$project = Join-Path $projectDir "GstarCadDrawingLibrary.csproj"
$msbuild = "C:\Windows\Microsoft.NET\Framework64\v4.0.30319\MSBuild.exe"

if (-not (Test-Path -LiteralPath $msbuild)) {
    throw ".NET Framework MSBuild was not found."
}

if ([string]::IsNullOrWhiteSpace($GstarCadInstallDir)) {
    $uninstallKeys = @(
        "HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\*",
        "HKLM:\SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall\*"
    )
    $installation = Get-ItemProperty $uninstallKeys -ErrorAction SilentlyContinue |
        Where-Object { $_.DisplayName -match "CAD 2024" -and $_.InstallLocation } |
        Select-Object -First 1
    if ($installation) {
        $GstarCadInstallDir = $installation.InstallLocation
    }
}

if ([string]::IsNullOrWhiteSpace($GstarCadInstallDir) -or
    -not (Test-Path -LiteralPath (Join-Path $GstarCadInstallDir "GcCoreMgd.dll"))) {
    throw "GstarCAD 2024 managed assemblies were not found. Use -GstarCadInstallDir."
}

$arguments = @(
    $project,
    "/t:Rebuild",
    "/p:Configuration=$Configuration",
    "/p:Platform=x64",
    "/p:GstarCadInstallDir=$GstarCadInstallDir",
    "/v:minimal"
)
& $msbuild $arguments
if ($LASTEXITCODE -ne 0) {
    throw "Build failed with exit code $LASTEXITCODE."
}

$package = Join-Path $projectDir "package"
New-Item -ItemType Directory -Force -Path $package | Out-Null
Copy-Item -LiteralPath (Join-Path $projectDir "bin\$Configuration\GstarCadDrawingLibrary.dll") -Destination $package -Force
Copy-Item -LiteralPath (Join-Path $projectDir "README.md") -Destination $package -Force

Write-Host "Build completed: $package"
