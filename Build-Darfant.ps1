param(
    [string] $Bundle = (Join-Path (Split-Path $PSScriptRoot) 'v2rayN-windows-64.zip'),
    [string] $Output = (Join-Path (Split-Path $PSScriptRoot) 'Darfant-windows-x64'),
    [string] $Dotnet = 'dotnet'
)

$ErrorActionPreference = 'Stop'
$Bundle = (Resolve-Path -LiteralPath $Bundle).Path
$Output = $ExecutionContext.SessionState.Path.GetUnresolvedProviderPathFromPSPath($Output)
if (Test-Path -LiteralPath $Output) {
    throw "Choose a new output folder: $Output"
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

    & tar.exe -xf $Bundle -C $staging
    if ($LASTEXITCODE -ne 0) {
        throw 'Could not extract the supplied v2rayN archive.'
    }
    Copy-Item -LiteralPath (Join-Path $staging 'v2rayN-windows-64\bin') -Destination $publish -Recurse
    Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'windows-assets\geoip-ir.srs') -Destination (Join-Path $publish 'bin\srss')
    Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'LICENSE') -Destination (Join-Path $publish 'LICENSE.txt')
    Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'Darfant-fa.md') -Destination (Join-Path $publish 'Darfant-fa.md')
    Move-Item -LiteralPath $publish -Destination $Output
}
finally {
    $resolvedStaging = (Resolve-Path -LiteralPath $staging).Path
    if ($resolvedStaging -eq $staging -and (Split-Path $resolvedStaging -Leaf) -match '^Darfant-[0-9a-f]{32}$') {
        Remove-Item -LiteralPath $resolvedStaging -Recurse -Force
    }
}
