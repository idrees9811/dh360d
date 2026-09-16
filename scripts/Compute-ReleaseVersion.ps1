# Computes the next release version from csproj + latest git tag.
# Writes version and skip outputs for GitHub Actions.
$ErrorActionPreference = 'Stop'

function Test-GitRef {
  param([string]$Ref)
  git rev-parse -q --verify $Ref 2>$null | Out-Null
  return ($LASTEXITCODE -eq 0)
}

function Set-GitHubOutput {
  param([hashtable]$Values)
  if (-not $env:GITHUB_OUTPUT) {
    return
  }

  $lines = foreach ($key in $Values.Keys) {
    "{0}={1}" -f $key, $Values[$key]
  }
  Add-Content -Path $env:GITHUB_OUTPUT -Value $lines -Encoding utf8
}

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
  $head = if (Test-GitRef 'HEAD') { git rev-parse HEAD } else { $null }

  if ($latestTag -and $head) {
    $tagVersion = [version]$latestTag.TrimStart('v')
    $tagCommit = git rev-list -n 1 $latestTag 2>$null
    if ($tagCommit -eq $head) {
      Write-Host "HEAD already tagged as $latestTag - skipping release."
      Set-GitHubOutput @{ skip = 'true' }
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

  if (Test-GitRef "refs/tags/$tag") {
    throw "Tag $tag already exists but points to a different commit."
  }

  Write-Host "Next release: $tag"
  Set-GitHubOutput @{
    version = $version
    tag     = $tag
    skip    = 'false'
  }
  exit 0
} finally {
  Pop-Location
}