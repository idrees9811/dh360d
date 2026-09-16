# Computes the next release version from csproj + latest git tag.
# Writes version and skip outputs for GitHub Actions.
$ErrorActionPreference = 'Stop'

$repoRoot = Resolve-Path (Join-Path $PSScriptRoot '..')
$projectFile = Join-Path $repoRoot 'Dh360dFeed.csproj'

[xml]$doc = Get-Content $projectFile
$csprojVersionText = ($doc.Project.PropertyGroup | Where-Object { $_.Version } | Select-Object -First 1).Version
if (-not $csprojVersionText) {
  throw "Version not found in $projectFile"
}

$csprojVersion = [version][string]$csprojVersionText

Push-Location $repoRoot
try {
  $latestTag = git tag --list 'v*' --sort=-v:refname | Select-Object -First 1

  $ErrorActionPreference = 'SilentlyContinue'
  $head = git rev-parse --verify HEAD 2>$null
  if ($LASTEXITCODE -ne 0) { $head = $null }
  $ErrorActionPreference = 'Stop'

  if ($latestTag -and $head) {
    $tagVersion = [version]$latestTag.TrimStart('v')
    $tagCommit = git rev-list -n 1 $latestTag 2>$null
    if ($tagCommit -eq $head) {
      Write-Host "HEAD already tagged as $latestTag - skipping release."
      if ($env:GITHUB_OUTPUT) {
        "skip=true" | Out-File -FilePath $env:GITHUB_OUTPUT -Append -Encoding utf8
      }
      exit 0
    }

    if ($csprojVersion.Major -gt $tagVersion.Major) {
      $next = [version]"$($csprojVersion.Major).$($csprojVersion.Minor).$($csprojVersion.Build)"
    } else {
      $next = [version]"$($tagVersion.Major).$($tagVersion.Minor + 1).0"
    }
  } else {
    $next = [version]"$($csprojVersion.Major).$($csprojVersion.Minor).$($csprojVersion.Build)"
  }

  $version = "$($next.Major).$($next.Minor).$($next.Build)"
  $tag = "v$version"

  $ErrorActionPreference = 'SilentlyContinue'
  git rev-parse -q --verify "refs/tags/$tag" 2>$null | Out-Null
  $tagExists = ($LASTEXITCODE -eq 0)
  $ErrorActionPreference = 'Stop'
  if ($tagExists) {
    throw "Tag $tag already exists but points to a different commit."
  }

  Write-Host "Next release: $tag"
  if ($env:GITHUB_OUTPUT) {
    "version=$version" | Out-File -FilePath $env:GITHUB_OUTPUT -Append -Encoding utf8
    "tag=$tag" | Out-File -FilePath $env:GITHUB_OUTPUT -Append -Encoding utf8
    "skip=false" | Out-File -FilePath $env:GITHUB_OUTPUT -Append -Encoding utf8
  } else {
    Write-Output $version
  }
} finally {
  Pop-Location
}