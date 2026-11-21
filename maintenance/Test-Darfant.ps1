param([string] $Dotnet = 'dotnet', [switch] $SkipMaintenanceTests)
. (Join-Path $PSScriptRoot 'Common.ps1')
$root = Split-Path $PSScriptRoot
$lock = Read-Dependencies $root
if (Invoke-DarfantGit $root @('diff', '--name-only', '--diff-filter=U')) { throw 'Resolve upstream merge conflicts before testing.' }
$heads = @(Invoke-DarfantGit $root @('rev-list', '-1', 'HEAD'))
$gitDir = Invoke-DarfantGit $root @('rev-parse', '--absolute-git-dir')
if (Test-Path -LiteralPath (Join-Path $gitDir 'MERGE_HEAD')) { $heads += Get-Content -LiteralPath (Join-Path $gitDir 'MERGE_HEAD') }
if ($heads -notcontains $lock.v2rayN.commit) {
    Invoke-DarfantGit $root @('merge-base', '--is-ancestor', $lock.v2rayN.commit, 'HEAD')
}
$workflows = @(Invoke-DarfantGit $root @('ls-files', '.github/workflows'))
foreach ($workflow in $workflows) {
    if ($workflow -notmatch '^.github/workflows/darfant-[^/]+\.yml$') { throw "Unexpected upstream workflow: $workflow" }
}

if (!$SkipMaintenanceTests) { & (Join-Path $PSScriptRoot 'tests/Test-UpstreamMerge.ps1') }
$project = Join-Path $root 'v2rayN/v2rayN/v2rayN.csproj'
& $Dotnet build $project -c Release
if ($LASTEXITCODE -ne 0) { throw 'The Windows build failed.' }
$metadata = & $Dotnet msbuild $project -nologo -p:Configuration=Release '-getProperty:AssemblyName,RootNamespace,Version,ApplicationManifest,TargetPath'
if ($LASTEXITCODE -ne 0) { throw 'Could not inspect the build metadata.' }
$properties = ($metadata -join "`n" | ConvertFrom-Json).Properties
[xml] $branding = Get-Content -LiteralPath (Join-Path $root 'Darfant/Branding.props') -Raw
if ($properties.AssemblyName -ne 'Darfant' -or $properties.RootNamespace -ne 'v2rayN' -or
    $properties.Version -ne $branding.Project.PropertyGroup.DarfantVersion -or
    $properties.ApplicationManifest -notlike '*Darfant*app.manifest' -or
    (Split-Path $properties.TargetPath -Leaf) -ne 'Darfant.dll' -or
    !(Test-Path -LiteralPath $properties.TargetPath)) { throw 'Darfant branding was lost during the upstream merge.' }
[xml] $manifest = Get-Content -LiteralPath $properties.ApplicationManifest -Raw
if (!$manifest.SelectSingleNode("//*[@level='requireAdministrator']")) { throw 'The Darfant administrator manifest was lost.' }
$tests = Join-Path $root 'v2rayN/ServiceLib.Tests/ServiceLib.Tests.csproj'
foreach ($filter in @('/*/ServiceLib.Tests.Common/ServerLabelsTests/*', '/*/ServiceLib.Tests.Services/SpeedtestServiceBatchTests/*', '/*/ServiceLib.Tests.Services/DarfantUpdateServiceTests/*')) {
    & $Dotnet run --project $tests -c Release -- --treenode-filter $filter
    if ($LASTEXITCODE -ne 0) { throw "Darfant regression tests failed: $filter" }
}
Write-Host 'Darfant build, branding and regression checks passed.'
