# Build DH360D.exe and optional Windows installer
param(
  [string]$Version
)

$ErrorActionPreference = 'Stop'
$here = Split-Path -Parent $MyInvocation.MyCommand.Path
$project = Join-Path $here 'Dh360dFeed.csproj'
$publish = Join-Path $here 'publish'
$driver = Join-Path $here 'driver'
$pwn = Join-Path $driver 'PawnIO_setup.exe'
$dist = Join-Path $here 'dist'

function Get-ProjectVersion {
  param([string]$ProjectPath)
  [xml]$doc = Get-Content $ProjectPath
  foreach ($group in @($doc.Project.PropertyGroup)) {
    if ($group.Version) {
      return [string]$group.Version
    }
  }

  throw "Version not found in $ProjectPath"
}

function Find-InnoSetupCompiler {
  foreach ($path in @(
      "${env:ProgramFiles(x86)}\Inno Setup 6\ISCC.exe",
      "$env:ProgramFiles\Inno Setup 6\ISCC.exe",
      "${env:LocalAppData}\Programs\Inno Setup 6\ISCC.exe"
    )) {
    if (Test-Path $path) { return $path }
  }

  $cmd = Get-Command ISCC.exe -ErrorAction SilentlyContinue
  if ($cmd) { return $cmd.Source }

  return $null
}

if (-not $Version) {
  $Version = Get-ProjectVersion $project
}

$versionParts = $Version.Split('.')
$assemblyVersion = if ($versionParts.Count -ge 3) { "$Version.0" } else { "$Version.0.0.0" }

New-Item -ItemType Directory -Force -Path $driver, $dist | Out-Null
if (-not (Test-Path $pwn)) {
  Write-Host 'Downloading PawnIO_setup.exe...'
  Invoke-WebRequest -Uri 'https://github.com/namazso/PawnIO.Setup/releases/download/2.2.0/PawnIO_setup.exe' -OutFile $pwn
}

Write-Host "Publishing DH360D.exe (version $Version)..."
dotnet publish $project -c Release -r win-x64 -o $publish --nologo `
  /p:Version=$Version `
  /p:AssemblyVersion=$assemblyVersion `
  /p:FileVersion=$assemblyVersion
if ($LASTEXITCODE -ne 0) {
  throw "dotnet publish failed with exit code $LASTEXITCODE"
}

$exe = Join-Path $publish 'DH360D.exe'
if (-not (Test-Path $exe)) { throw "Build failed - $exe not found" }

Write-Host "Built: $exe"

$iscc = Find-InnoSetupCompiler
if ($iscc) {
  Write-Host "Building installer with: $iscc"
  & $iscc "/DMyAppVersion=$Version" (Join-Path $here 'installer\dh360d.iss')
  Get-ChildItem $dist -Filter '*.exe' | ForEach-Object { Write-Host "Installer: $($_.FullName)" }
} else {
  Write-Host 'Inno Setup not found - skipped installer.'
  Write-Host '  DH360D.exe in publish\ is ready to run. Inno only builds the Setup wizard for GitHub Releases.'
  Write-Host '  Install: winget install JRSoftware.InnoSetup'
  Write-Host '  Then re-run .\build.ps1 to produce dist\DH360D-Setup-*.exe'
}
