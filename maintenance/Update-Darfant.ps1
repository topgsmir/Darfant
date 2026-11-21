[CmdletBinding()]
param(
    [ValidateSet('Check', 'V2rayN', 'Xray', 'Bundle')][string] $Component = 'Check',
    [string] $Version = 'latest',
    [switch] $Continue,
    [switch] $Abort,
    [string] $RepositoryRoot = (Split-Path $PSScriptRoot)
)

. (Join-Path $PSScriptRoot 'Common.ps1')
$root = (Resolve-Path -LiteralPath $RepositoryRoot).Path
$lock = Read-Dependencies $root
if ($Continue -and $Abort) { throw 'Choose Continue or Abort.' }
if (($Continue -or $Abort) -and $Component -ne 'V2rayN') { throw 'Continue and Abort apply only to V2rayN merges.' }

if ($Component -eq 'Check') {
    $gui = Get-OfficialRelease $lock.v2rayN.repository
    $core = Get-OfficialRelease $lock.xray.repository
    [pscustomobject]@{ Component = 'v2rayN source'; Current = $lock.v2rayN.tag; Latest = $gui.tag_name }
    [pscustomobject]@{ Component = 'Xray'; Current = $lock.xray.version; Latest = $core.tag_name }
    [pscustomobject]@{ Component = 'Windows bundle'; Current = $lock.bundle.tag; Latest = $gui.tag_name }
    return
}

$gitDir = Invoke-DarfantGit $root @('rev-parse', '--absolute-git-dir')
$pendingPath = Join-Path $gitDir 'darfant-upstream-update.json'
if ($Abort) {
    Invoke-DarfantGit $root @('merge', '--abort')
    if (Test-Path -LiteralPath $pendingPath) { Remove-Item -LiteralPath $pendingPath }
    Write-Host 'Upstream merge aborted. The update branch is still checked out.'
    return
}
if ($Continue) {
    if (!(Test-Path -LiteralPath $pendingPath)) { throw 'No Darfant upstream merge to continue.' }
    $pending = Get-Content -LiteralPath $pendingPath -Raw | ConvertFrom-Json
    $mergeHead = Invoke-DarfantGit $root @('rev-parse', '--verify', 'MERGE_HEAD')
    if ($mergeHead -ne $pending.commit) { throw 'The pending merge does not match the recorded upstream commit.' }
    if (Invoke-DarfantGit $root @('diff', '--name-only', '--diff-filter=U')) { throw 'Resolve and stage the remaining conflicts first.' }
    $lock.v2rayN.tag = $pending.tag
    $lock.v2rayN.commit = $pending.commit
    Write-Dependencies $root $lock
    Invoke-DarfantGit $root @('add', '--', 'maintenance/dependencies.json')
    Write-Host 'Conflicts resolved. Run maintenance/Test-Darfant.ps1, review the changes, then git commit to finish the merge.'
    return
}

if (Invoke-DarfantGit $root @('status', '--porcelain')) { throw 'Commit or stash your existing changes before starting an update.' }
if (Test-Path -LiteralPath (Join-Path $gitDir 'MERGE_HEAD')) { throw 'Finish the current merge first.' }

if ($Component -eq 'V2rayN') {
    if ($Version -eq 'latest') { $Version = (Get-OfficialRelease $lock.v2rayN.repository).tag_name }
    if ($Version -notmatch '^v?\d+\.\d+\.\d+(?:[-.][0-9A-Za-z.-]+)?$') { throw 'Invalid v2rayN version tag.' }
    if ($Version -eq $lock.v2rayN.tag) { Write-Host 'This v2rayN version is already integrated.'; return }
    $remotes = @(Invoke-DarfantGit $root @('remote'))
    if ($remotes -notcontains 'upstream') {
        Invoke-DarfantGit $root @('remote', 'add', 'upstream', $lock.v2rayN.gitUrl)
    }
    elseif ((Invoke-DarfantGit $root @('remote', 'get-url', 'upstream')) -ne $lock.v2rayN.gitUrl) {
        throw 'The upstream remote does not match maintenance/dependencies.json.'
    }
    $ref = "refs/remotes/upstream/tags/$Version"
    Invoke-DarfantGit $root @('fetch', '--no-tags', '--filter=blob:none', 'upstream', "refs/tags/${Version}:$ref")
    $commit = Invoke-DarfantGit $root @('rev-parse', "$ref^{commit}")
    Invoke-DarfantGit $root @('merge-base', '--is-ancestor', $lock.v2rayN.commit, 'HEAD')
    Invoke-DarfantGit $root @('merge-base', '--is-ancestor', $lock.v2rayN.commit, $commit)
    $start = Invoke-DarfantGit $root @('rev-parse', 'HEAD')
    $branch = "update/v2rayn-$Version-$([DateTime]::UtcNow.ToString('yyyyMMddHHmmss'))"
    Invoke-DarfantGit $root @('switch', '-c', $branch)
    $pending = @{ tag = $Version; commit = $commit; previous = $start }
    [IO.File]::WriteAllText($pendingPath, ($pending | ConvertTo-Json), [Text.UTF8Encoding]::new($false))
    & git -c "safe.directory=$($root.Replace('\', '/'))" -C $root merge --no-commit --no-ff $commit
    $mergeExit = $LASTEXITCODE
    if ($mergeExit -ne 0 -and !(Test-Path -LiteralPath (Join-Path $gitDir 'MERGE_HEAD'))) { throw 'Upstream merge could not start.' }

    # Preserve Darfant's README and CI, including when upstream adds new workflow files.
    # Remove the merged index entries first: restore alone cannot remove modify/delete conflicts absent in $start.
    Invoke-DarfantGit $root @('rm', '-r', '-f', '--ignore-unmatch', '--', '.github/workflows')
    Invoke-DarfantGit $root @('restore', "--source=$start", '--staged', '--worktree', '--', 'README.md', '.github/workflows')
    Invoke-DarfantGit $root @('rm', '--ignore-unmatch', '-f', '--', '.github/dependabot.yml')
    $readme = (Invoke-DarfantGit $root @('show', "${commit}:README.md")) -join "`n"
    [IO.File]::WriteAllText((Join-Path $root 'README.upstream.md'), $readme + "`n", [Text.UTF8Encoding]::new($false))
    $lock.v2rayN.tag = $Version
    $lock.v2rayN.commit = $commit
    Write-Dependencies $root $lock
    Invoke-DarfantGit $root @('add', '--', 'README.upstream.md', 'maintenance/dependencies.json')
    $conflicts = @(Invoke-DarfantGit $root @('diff', '--name-only', '--diff-filter=U'))
    if ($conflicts.Count) {
        Write-Host ($conflicts -join "`n")
        throw 'Resolve and stage these files, then run Update-Darfant.ps1 -Component V2rayN -Continue. Use -Abort to undo the merge.'
    }
    Write-Host 'Upstream source merged on an update branch. Run maintenance/Test-Darfant.ps1, review the changes, then git commit.'
    return
}

$repository = $lock.xray.repository
$assetName = $lock.xray.asset
if ($Component -eq 'Bundle') { $repository = $lock.bundle.repository; $assetName = $lock.bundle.asset }
$release = Get-OfficialRelease $repository $Version
$asset = Get-ReleaseAsset $release $assetName
$dependency = [pscustomobject]@{ repository = $repository; tag = $release.tag_name; asset = $assetName; sha256 = $asset.digest.Substring(7) }
if ($Component -eq 'Xray' -and $lock.xray.source -eq 'release' -and
    $lock.xray.tag -eq $release.tag_name -and $lock.xray.sha256 -eq $dependency.sha256) {
    Write-Host 'This Xray release is already pinned.'; return
}
if ($Component -eq 'Bundle' -and $lock.bundle.tag -eq $release.tag_name -and $lock.bundle.sha256 -eq $dependency.sha256) {
    Write-Host 'This Windows bundle is already pinned.'; return
}
$archive = Get-DependencyArchive $root $dependency
$temporary = Join-Path ([IO.Path]::GetTempPath()) ('Darfant-' + [Guid]::NewGuid().ToString('N'))
try {
    Expand-Dependency $archive $temporary
    if ($Component -eq 'Xray') {
        $exe = Join-Path $temporary 'xray.exe'
        if (!(Test-Path -LiteralPath $exe)) { throw 'The Xray archive layout changed; review the packaging script.' }
        $output = & $exe version
        $number = $release.tag_name.TrimStart('v')
        if ($LASTEXITCODE -ne 0 -or ($output -join "`n") -notmatch "(?m)^Xray $([regex]::Escape($number))\s") {
            throw 'Xray did not report the expected version.'
        }
        Invoke-DarfantGit $root @('switch', '-c', "update/xray-$($release.tag_name)-$([DateTime]::UtcNow.ToString('yyyyMMddHHmmss'))")
        $lock.xray.source = 'release'
        $lock.xray.version = $number
        $lock.xray.tag = $release.tag_name
        $lock.xray.sha256 = $dependency.sha256
        $lock.xray.executableSha256 = (Get-FileHash -LiteralPath $exe -Algorithm SHA256).Hash.ToLowerInvariant()
    }
    else {
        $bundleRoot = Join-Path $temporary $lock.bundle.root
        foreach ($path in @('bin/xray/xray.exe', 'bin/sing_box/sing-box.exe', 'bin/mihomo/mihomo.exe')) {
            if (!(Test-Path -LiteralPath (Join-Path $bundleRoot $path))) { throw "The bundle layout changed: $path" }
        }
        Invoke-DarfantGit $root @('switch', '-c', "update/bundle-$($release.tag_name)-$([DateTime]::UtcNow.ToString('yyyyMMddHHmmss'))")
        $lock.bundle.tag = $release.tag_name
        $lock.bundle.sha256 = $dependency.sha256
        if ($lock.xray.source -eq 'bundle') {
            $exe = Join-Path $bundleRoot 'bin/xray/xray.exe'
            $output = & $exe version
            if ($LASTEXITCODE -ne 0 -or ($output -join "`n") -notmatch '(?m)^Xray ([0-9.]+)\s') { throw 'Cannot identify bundled Xray version.' }
            $lock.xray.version = $Matches[1]
            $lock.xray.executableSha256 = (Get-FileHash -LiteralPath $exe -Algorithm SHA256).Hash.ToLowerInvariant()
        }
    }
    Write-Dependencies $root $lock
    Invoke-DarfantGit $root @('add', '--', 'maintenance/dependencies.json')
    Write-Host 'Dependency pinned on an update branch. Run maintenance/Test-Darfant.ps1 and Build-Darfant.ps1, then commit.'
}
finally { Remove-DarfantTemporaryDirectory $temporary }
