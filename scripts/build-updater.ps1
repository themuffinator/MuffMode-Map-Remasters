param(
  [string]$Version,
  [string]$OutDir = "dist\updater",
  [string]$Configuration = "Release"
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
$appVersion = $versionValue.TrimStart('v', 'V')
$project = Join-Path $root "tools\updater\MuffModeUpdater.csproj"
$outRoot = Join-Path $root $OutDir

if (-not (Test-Path $project)) {
  throw "Missing updater project: $project"
}

New-Item -ItemType Directory -Path $outRoot -Force | Out-Null

$publishArgs = @(
  "publish",
  $project,
  "-c", $Configuration,
  "-r", "win-x64",
  "--self-contained", "true",
  "/p:PublishSingleFile=true",
  "/p:IncludeNativeLibrariesForSelfExtract=true",
  "/p:Version=$appVersion",
  "/p:FileVersion=$appVersion",
  "/p:InformationalVersion=$versionValue",
  "-o", $outRoot
)

& dotnet @publishArgs

$sourceExe = Join-Path $outRoot "MuffModeUpdater.exe"
$targetExe = Join-Path $outRoot "MuffMode-Map-Remasters-Updater.exe"

if (-not (Test-Path $sourceExe)) {
  throw "Updater build output missing: $sourceExe"
}

Copy-Item -Path $sourceExe -Destination $targetExe -Force
Write-Host "Updater output: $targetExe"
