Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

function Invoke-DarfantGit {
    param([string] $Root, [string[]] $Arguments)
    $result = & git -c "safe.directory=$($Root.Replace('\', '/'))" -C $Root @Arguments
    if ($LASTEXITCODE -ne 0) { throw "Git failed: $($Arguments -join ' ')" }
    return $result
}

function Read-Dependencies {
    param([string] $Root)
    $lock = Get-Content -LiteralPath (Join-Path $Root 'maintenance/dependencies.json') -Raw | ConvertFrom-Json
    if ($lock.schemaVersion -ne 1) { throw 'Unsupported dependency manifest version.' }
    return $lock
}

function Write-Dependencies {
    param([string] $Root, $Dependencies)
    $json = ($Dependencies | ConvertTo-Json -Depth 12) + "`n"
    [IO.File]::WriteAllText((Join-Path $Root 'maintenance/dependencies.json'), $json, [Text.UTF8Encoding]::new($false))
}

function Get-OfficialRelease {
    param([string] $Repository, [string] $Version = 'latest')
    if ($Repository -notin @('2dust/v2rayN', 'XTLS/Xray-core')) { throw 'Unexpected upstream repository.' }
    $headers = @{ 'User-Agent' = 'Darfant-maintenance'; Accept = 'application/vnd.github+json' }
    $token = $env:GH_TOKEN
    if (!$token) { $token = $env:GITHUB_TOKEN }
    if ($token) { $headers.Authorization = "Bearer $token" }
    $suffix = 'latest'
    if ($Version -ne 'latest') { $suffix = 'tags/' + [Uri]::EscapeDataString($Version) }
    $release = Invoke-RestMethod -Uri "https://api.github.com/repos/$Repository/releases/$suffix" -Headers $headers
    if ($release.draft -or $release.prerelease) { throw 'Choose a stable, published upstream release.' }
    if ($release.tag_name -notmatch '^v?\d+\.\d+\.\d+(?:[-.][0-9A-Za-z.-]+)?$') { throw 'Unexpected upstream tag.' }
    return $release
}

function Get-ReleaseAsset {
    param($Release, [string] $Name)
    $assets = @($Release.assets | Where-Object name -eq $Name)
    if ($assets.Count -ne 1 -or $assets[0].digest -notmatch '^sha256:[0-9a-fA-F]{64}$') {
        throw "Release asset $Name is missing or has no published SHA-256 digest. Do not guess its checksum."
    }
    return $assets[0]
}

function Assert-Sha256 {
    param([string] $Path, [string] $Expected)
    if ($Expected -notmatch '^[0-9a-fA-F]{64}$') { throw "Invalid SHA-256 for $Path" }
    if ((Get-FileHash -LiteralPath $Path -Algorithm SHA256).Hash -ne $Expected) {
        throw "SHA-256 verification failed: $Path"
    }
}

function Get-DependencyArchive {
    param([string] $Root, $Dependency, [string] $LocalPath)
    if ($LocalPath) {
        $path = (Resolve-Path -LiteralPath $LocalPath).Path
        Assert-Sha256 $path $Dependency.sha256
        return $path
    }
    if ($Dependency.repository -notin @('2dust/v2rayN', 'XTLS/Xray-core') -or
        $Dependency.tag -notmatch '^v?\d+\.\d+\.\d+(?:[-.][0-9A-Za-z.-]+)?$' -or
        $Dependency.asset -notmatch '^[A-Za-z0-9._-]+\.zip$') { throw 'Invalid dependency archive.' }
    $cache = Join-Path $Root '.darfant-cache'
    New-Item -ItemType Directory -Force -Path $cache | Out-Null
    $path = Join-Path $cache ($Dependency.sha256 + '-' + $Dependency.asset)
    if (!(Test-Path -LiteralPath $path)) {
        $temporary = "$path.$([Guid]::NewGuid().ToString('N')).download"
        try {
            $url = "https://github.com/$($Dependency.repository)/releases/download/$($Dependency.tag)/$($Dependency.asset)"
            Write-Host "Downloading $($Dependency.repository) $($Dependency.tag) / $($Dependency.asset)"
            & curl.exe --fail --location --retry 2 --output $temporary $url
            if ($LASTEXITCODE -ne 0) { throw 'Dependency download failed.' }
            Assert-Sha256 $temporary $Dependency.sha256
            Move-Item -LiteralPath $temporary -Destination $path
        }
        finally { if (Test-Path -LiteralPath $temporary) { Remove-Item -LiteralPath $temporary -Force } }
    }
    Assert-Sha256 $path $Dependency.sha256
    return $path
}

function Expand-Dependency {
    param([string] $Archive, [string] $Destination)
    New-Item -ItemType Directory -Force -Path $Destination | Out-Null
    & tar.exe -xf $Archive -C $Destination
    if ($LASTEXITCODE -ne 0) { throw "Could not extract $Archive" }
}

function Remove-DarfantTemporaryDirectory {
    param([string] $Path)
    if (!(Test-Path -LiteralPath $Path)) { return }
    $resolved = (Resolve-Path -LiteralPath $Path).Path
    $temp = [IO.Path]::GetFullPath([IO.Path]::GetTempPath()).TrimEnd('\')
    if ((Split-Path $resolved) -ne $temp -or (Split-Path $resolved -Leaf) -notmatch '^Darfant-[0-9a-f]{32}$') {
        throw "Refusing to remove an unexpected temporary path: $resolved"
    }
    Remove-Item -LiteralPath $resolved -Recurse -Force
}
