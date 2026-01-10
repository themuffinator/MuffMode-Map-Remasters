param(
  [string]$Version,
  [string]$OutDir = "dist",
  [string]$IsccPath
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

function Resolve-Iscc {
  param([string]$UserPath)

  if ($UserPath) {
    return $UserPath
  }

  $cmd = Get-Command iscc -ErrorAction SilentlyContinue
  if ($cmd) {
    return $cmd.Source
  }

  $cmd = Get-Command ISCC -ErrorAction SilentlyContinue
  if ($cmd) {
    return $cmd.Source
  }

  $defaultPath = "C:\Program Files (x86)\Inno Setup 6\ISCC.exe"
  if (Test-Path $defaultPath) {
    return $defaultPath
  }

  return $null
}

$root = Resolve-Path (Join-Path $PSScriptRoot "..")
$versionValue = Get-ReleaseVersion -RootDir $root
$appVersion = $versionValue.TrimStart('v', 'V')
$installerScript = Join-Path $root "installer\muffmode-remasters.iss"
$updaterScript = Join-Path $root "scripts\build-updater.ps1"
$resolvedIscc = Resolve-Iscc -UserPath $IsccPath

if (-not (Test-Path $installerScript)) {
  throw "Missing installer script: $installerScript"
}

if (-not $resolvedIscc) {
  throw "ISCC.exe not found. Install Inno Setup 6 or pass -IsccPath."
}

$updaterExe = Join-Path $root "dist\updater\MuffMode-Map-Remasters-Updater.exe"
if (Test-Path $updaterScript) {
  & $updaterScript -Version $versionValue
} else {
  throw "Missing updater build script: $updaterScript"
}

if (-not (Test-Path $updaterExe)) {
  throw "Updater executable not found: $updaterExe"
}

$outRoot = Join-Path $root $OutDir
New-Item -ItemType Directory -Path $outRoot -Force | Out-Null

$baseName = "MuffMode-Map-Remasters-$versionValue-setup"

& $resolvedIscc $installerScript "/DAppVersion=$appVersion" "/O$outRoot" "/F$baseName"

Write-Host "Installer output: $outRoot\$baseName.exe"
