[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)][string]$AppBuildVdf,
    [string]$ContentDir
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

if ([string]::IsNullOrEmpty($env:STEAM_USERNAME)) {
    throw 'STEAM_USERNAME is not set.'
}

$steamcmd = if ($env:STEAMCMD) { $env:STEAMCMD } else { 'steamcmd.exe' }
$resolved = Get-Command $steamcmd -ErrorAction SilentlyContinue
if (-not $resolved) {
    throw "steamcmd not found at '$steamcmd'. Set STEAMCMD to the full path of steamcmd.exe."
}
$steamcmdPath = $resolved.Source

if (-not (Test-Path -LiteralPath $AppBuildVdf -PathType Leaf)) {
    throw "App build script not found: $AppBuildVdf"
}
$appVdf = (Resolve-Path -LiteralPath $AppBuildVdf).Path

if ($ContentDir) {
    if (-not (Test-Path -LiteralPath $ContentDir -PathType Container)) {
        throw "Content folder not found: $ContentDir"
    }
    $patterns = @('steam_appid.txt', '*_BurstDebugInformation_DoNotShip', '*_BackUpThisFolder_ButDontShipItWithYourGame')
    $leftovers = @(Get-ChildItem -LiteralPath $ContentDir -Recurse -Force | Where-Object {
        $name = $_.Name
        @($patterns | Where-Object { $name -like $_ }).Count -gt 0
    })
    if ($leftovers.Count -gt 0) {
        $leftovers | ForEach-Object { Write-Error "Non-shipping item in content folder: $($_.FullName)" -ErrorAction Continue }
        exit 65
    }
}

if ($env:STEAM_CONFIG_VDF_BASE64) {
    $configDir = if ($env:STEAM_CONFIG_DIR) { $env:STEAM_CONFIG_DIR } else { Join-Path (Split-Path -Parent $steamcmdPath) 'config' }
    New-Item -ItemType Directory -Force -Path $configDir | Out-Null
    [System.IO.File]::WriteAllBytes((Join-Path $configDir 'config.vdf'), [System.Convert]::FromBase64String($env:STEAM_CONFIG_VDF_BASE64))
}

$arguments = @('+@ShutdownOnFailedCommand', '1', '+@NoPromptForPassword', '1')
if ($env:STEAM_GUARD_CODE) {
    $arguments += @('+set_steam_guard_code', $env:STEAM_GUARD_CODE)
}
$arguments += @('+login', $env:STEAM_USERNAME)
if ($env:STEAM_PASSWORD) {
    $arguments += $env:STEAM_PASSWORD
}
$arguments += @('+run_app_build', $appVdf, '+quit')

& $steamcmdPath @arguments
if ($LASTEXITCODE -ne 0) {
    throw "steamcmd exited with code $LASTEXITCODE."
}
