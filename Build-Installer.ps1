param(
    [Parameter(Mandatory)][string] $Package,
    [string] $Output = (Join-Path (Split-Path $PSScriptRoot) 'Darfant-installer'),
    [Parameter(Mandatory)][string] $Compiler
)
. (Join-Path $PSScriptRoot 'maintenance/Common.ps1')
$Package = (Resolve-Path -LiteralPath $Package).Path
$Compiler = (Resolve-Path -LiteralPath $Compiler).Path
$Output = $ExecutionContext.SessionState.Path.GetUnresolvedProviderPathFromPSPath($Output)
if (Test-Path -LiteralPath $Output) { throw "Choose a new installer output folder: $Output" }
$tool = Get-Content -LiteralPath (Join-Path $PSScriptRoot 'installer/toolchain.json') -Raw | ConvertFrom-Json
$compilerVersion = & $Compiler --version
if ($LASTEXITCODE -ne 0 -or $compilerVersion -ne $tool.version) { throw "Use Inno Setup $($tool.version)." }
[xml] $branding = Get-Content -LiteralPath (Join-Path $PSScriptRoot 'Darfant/Branding.props') -Raw
$version = $branding.Project.PropertyGroup.DarfantVersion
foreach ($required in @('Darfant.exe', 'LICENSE.txt', 'Darfant-fa.md', 'Darfant-dependencies.json', 'bin/xray/xray.exe', 'bin/sing_box/sing-box.exe', 'bin/mihomo/mihomo.exe', 'bin/srss/geoip-ir.srs', 'Resources/Flags/LICENSE.txt')) {
    if (!(Test-Path -LiteralPath (Join-Path $Package $required))) { throw "The portable package is incomplete: $required" }
}
if ((Get-Item -LiteralPath (Join-Path $Package 'Darfant.exe')).VersionInfo.FileVersion -ne "$version.0") { throw 'The package version does not match Darfant/Branding.props.' }
foreach ($directory in Get-ChildItem -LiteralPath $Package -Directory) {
    if ($directory.Name -like 'gui*') { throw 'Build the installer from a clean package; never distribute user data.' }
}
$manifest = [Text.Encoding]::UTF8.GetString([IO.File]::ReadAllBytes((Join-Path $Package 'Darfant.exe')))
if ($manifest -notmatch 'requestedExecutionLevel\s+level="requireAdministrator"') { throw 'Darfant.exe lost its administrator manifest.' }
& $Compiler "/DPayloadDir=$Package" "/DAppVersion=$version" "/DInstallerOutputDir=$Output" (Join-Path $PSScriptRoot 'installer/Darfant.iss')
if ($LASTEXITCODE -ne 0) { throw 'Installer compilation failed.' }
$installer = Join-Path $Output 'Darfant-Setup-x64.exe'
if (!(Test-Path -LiteralPath $installer)) { throw 'The installer was not produced.' }
if (@(Get-ChildItem -LiteralPath $Output -File).Count -ne 1) { throw 'The installer must be a single EXE file.' }
Write-Host "Installer created: $installer"
