param(
    [string] $Bundle,
    [string] $Output = (Join-Path (Split-Path $PSScriptRoot) 'Darfant-windows-x64'),
    [string] $Dotnet = 'dotnet',
    [string] $XrayArchive
)

. (Join-Path $PSScriptRoot 'maintenance/Common.ps1')
$dependencies = Read-Dependencies $PSScriptRoot
$Output = $ExecutionContext.SessionState.Path.GetUnresolvedProviderPathFromPSPath($Output)
if (Test-Path -LiteralPath $Output) {
    throw "Choose a new output folder: $Output"
}
if (!$Bundle) {
    $supplied = Join-Path (Split-Path $PSScriptRoot) 'v2rayN-windows-64.zip'
    if ((Test-Path -LiteralPath $supplied) -and
        (Get-FileHash -LiteralPath $supplied -Algorithm SHA256).Hash -eq $dependencies.bundle.sha256) { $Bundle = $supplied }
}
$Bundle = Get-DependencyArchive $PSScriptRoot $dependencies.bundle $Bundle
if ($dependencies.xray.source -eq 'release') {
    $XrayArchive = Get-DependencyArchive $PSScriptRoot $dependencies.xray $XrayArchive
}
elseif ($dependencies.xray.source -ne 'bundle' -or $XrayArchive) {
    throw 'To replace bundled Xray, pin a release with maintenance/Update-Darfant.ps1 -Component Xray first.'
}

$staging = Join-Path ([IO.Path]::GetTempPath()) ("Darfant-" + [Guid]::NewGuid().ToString('N'))
$project = Join-Path $PSScriptRoot 'v2rayN\v2rayN\v2rayN.csproj'
New-Item -ItemType Directory -Path $staging | Out-Null
try {
    $publish = Join-Path $staging 'Darfant'
    & $Dotnet publish $project -c Release -r win-x64 --self-contained true -o $publish
    if ($LASTEXITCODE -ne 0) {
        throw 'Darfant publish failed.'
    }

    $bundleStaging = Join-Path $staging 'bundle'
    Expand-Dependency $Bundle $bundleStaging
    Copy-Item -LiteralPath (Join-Path $bundleStaging "$($dependencies.bundle.root)\bin") -Destination $publish -Recurse
    if ($dependencies.xray.source -eq 'release') {
        $xrayStaging = Join-Path $staging 'xray'
        Expand-Dependency $XrayArchive $xrayStaging
        if (!(Test-Path -LiteralPath (Join-Path $xrayStaging 'xray.exe'))) { throw 'The Xray release archive layout changed.' }
        Copy-Item -Path (Join-Path $xrayStaging '*') -Destination (Join-Path $publish 'bin\xray') -Recurse -Force
    }
    $xrayExecutable = Join-Path $publish 'bin\xray\xray.exe'
    Assert-Sha256 $xrayExecutable $dependencies.xray.executableSha256
    $xrayVersion = & $xrayExecutable version
    if ($LASTEXITCODE -ne 0 -or ($xrayVersion -join "`n") -notmatch "(?m)^Xray $([regex]::Escape($dependencies.xray.version))\s") {
        throw 'The packaged Xray version does not match maintenance/dependencies.json.'
    }
    Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'windows-assets\geoip-ir.srs') -Destination (Join-Path $publish 'bin\srss')
    Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'LICENSE') -Destination (Join-Path $publish 'LICENSE.txt')
    Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'Darfant-fa.md') -Destination (Join-Path $publish 'Darfant-fa.md')
    Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'maintenance\dependencies.json') -Destination (Join-Path $publish 'Darfant-dependencies.json')
    $manifest = [Text.Encoding]::UTF8.GetString([IO.File]::ReadAllBytes((Join-Path $publish 'Darfant.exe')))
    if ($manifest -notmatch 'requestedExecutionLevel\s+level="requireAdministrator"') { throw 'The administrator manifest is missing.' }
    Move-Item -LiteralPath $publish -Destination $Output
}
finally {
    Remove-DarfantTemporaryDirectory $staging
}
