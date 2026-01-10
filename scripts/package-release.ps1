param(
  [string]$Version,
  [string]$OutDir = "dist"
)

$ErrorActionPreference = "Stop"

function Get-ReleaseVersion {
  param([string]$RootDir)

  if ($Version) {
    return $Version
  }

  $gitVersion = $null
  try {
    $gitVersion = (& git -C $RootDir describe --tags --abbrev=0 2>$null).Trim()
  } catch {
    $gitVersion = $null
  }

  if ($gitVersion) {
    return $gitVersion
  }

  return "0.0.0-dev"
}

$root = Resolve-Path (Join-Path $PSScriptRoot "..")
$versionValue = Get-ReleaseVersion -RootDir $root
$outRoot = Join-Path $root $OutDir
New-Item -ItemType Directory -Path $outRoot -Force | Out-Null

function New-MapsPackage {
  param(
    [string]$SourceDir,
    [string]$Suffix
  )

  if (-not (Test-Path $SourceDir)) {
    throw "Missing maps source. Expected: $SourceDir"
  }

  $mapFiles = Get-ChildItem -Path $SourceDir -Filter "*.bsp"
  if ($mapFiles.Count -eq 0) {
    throw "No .bsp files found in $SourceDir"
  }

  $staging = Join-Path $outRoot ("staging-" + $Suffix)
  if (Test-Path $staging) {
    Remove-Item -Path $staging -Recurse -Force
  }

  $targetMaps = Join-Path $staging "baseq2\maps"
  New-Item -ItemType Directory -Path $targetMaps -Force | Out-Null

  Copy-Item -Path (Join-Path $SourceDir "*.bsp") -Destination $targetMaps -Force

  $zipName = "MuffMode-Map-Remasters-$versionValue-$Suffix.zip"
  $zipPath = Join-Path $outRoot $zipName

  if (Test-Path $zipPath) {
    Remove-Item -Path $zipPath -Force
  }

  Compress-Archive -Path (Join-Path $staging "*") -DestinationPath $zipPath
  Write-Host "Created: $zipPath"
}

$finalSource = Join-Path $root "finals\maps"
New-MapsPackage -SourceDir $finalSource -Suffix "final"

$devSource = Join-Path $root "dev\maps"
if (Test-Path $devSource) {
  $devFiles = Get-ChildItem -Path $devSource -Filter "*.bsp" -ErrorAction SilentlyContinue
  if ($devFiles.Count -gt 0) {
    New-MapsPackage -SourceDir $devSource -Suffix "dev"
  } else {
    Write-Host "No dev .bsp files found in $devSource. Skipping dev package."
  }
} else {
  Write-Host "Missing dev/maps. Skipping dev package."
}
