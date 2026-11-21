# Integration checks with disposable local upstream/fork repositories; no network needed.
. (Join-Path (Split-Path $PSScriptRoot) 'Common.ps1')
$scriptRoot = Split-Path $PSScriptRoot
$temporary = Join-Path ([IO.Path]::GetTempPath()) ('Darfant-' + [Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $temporary | Out-Null
function Write-FixtureFile([string] $Root, [string] $Name, [string] $Content) {
    $path = Join-Path $Root $Name
    New-Item -ItemType Directory -Force -Path (Split-Path $path) | Out-Null
    [IO.File]::WriteAllText($path, $Content, [Text.UTF8Encoding]::new($false))
}
function Commit-Fixture([string] $Root) {
    Invoke-DarfantGit $Root @('add', '-A') | Out-Null
    Invoke-DarfantGit $Root @('commit', '-m', 'Fixture change') | Out-Null
}
function Initialize-FixtureGit([string] $Root) {
    Invoke-DarfantGit $Root @('config', 'user.name', 'Darfant tests')
    Invoke-DarfantGit $Root @('config', 'user.email', 'darfant-tests@example.invalid')
    Invoke-DarfantGit $Root @('config', 'commit.gpgsign', 'false')
    Invoke-DarfantGit $Root @('config', 'core.autocrlf', 'false')
}
try {
    $upstream = Join-Path $temporary 'upstream'
    $fork = Join-Path $temporary 'fork'
    New-Item -ItemType Directory -Path $upstream | Out-Null
    Invoke-DarfantGit $upstream @('init', '-b', 'main') | Out-Null
    Initialize-FixtureGit $upstream
    Write-FixtureFile $upstream 'README.md' "Upstream original`n"
    Write-FixtureFile $upstream 'Shared.cs' "value = original;`n"
    Write-FixtureFile $upstream 'Independent.cs' "version = 1;`n"
    Write-FixtureFile $upstream '.github/workflows/upstream.yml' "upstream: 1`n"
    Commit-Fixture $upstream
    Invoke-DarfantGit $upstream @('tag', '1.0.0')
    $base = Invoke-DarfantGit $upstream @('rev-parse', 'HEAD')
    Invoke-DarfantGit $temporary @('-c', 'core.autocrlf=false', 'clone', $upstream, $fork) | Out-Null
    Initialize-FixtureGit $fork
    Invoke-DarfantGit $fork @('rm', '.github/workflows/upstream.yml') | Out-Null
    Write-FixtureFile $fork '.github/workflows/darfant-windows.yml' "darfant: 1`n"
    Write-FixtureFile $fork 'README.md' "Darfant instructions`n"
    Write-FixtureFile $fork 'Shared.cs' "value = darfant;`n"
    $lock = Read-Dependencies (Split-Path $scriptRoot)
    $lock.v2rayN.gitUrl = $upstream
    $lock.v2rayN.tag = '1.0.0'
    $lock.v2rayN.commit = $base
    New-Item -ItemType Directory -Path (Join-Path $fork 'maintenance') | Out-Null
    Write-Dependencies $fork $lock
    Commit-Fixture $fork

    # Dirty trees must never be overwritten.
    Write-FixtureFile $fork 'local.txt' 'uncommitted'
    $rejected = $false
    try { & (Join-Path $scriptRoot 'Update-Darfant.ps1') -Component V2rayN -Version '1.0.1' -RepositoryRoot $fork }
    catch { $rejected = $_.Exception.Message -like '*Commit or stash*' }
    if (!$rejected) { throw 'A dirty working tree was not rejected.' }
    Remove-Item -LiteralPath (Join-Path $fork 'local.txt')

    # A clean merge keeps branding and rejects newly introduced upstream automation.
    Write-FixtureFile $upstream 'Independent.cs' "version = 2;`n"
    Write-FixtureFile $upstream 'README.md' "Upstream updated`n"
    Write-FixtureFile $upstream '.github/workflows/new-upstream.yml' "upstream: 2`n"
    Write-FixtureFile $upstream '.github/workflows/upstream.yml' "upstream: changed`n"
    Write-FixtureFile $upstream '.github/dependabot.yml' "version: 2`n"
    Commit-Fixture $upstream
    Invoke-DarfantGit $upstream @('tag', '1.0.1')
    & (Join-Path $scriptRoot 'Update-Darfant.ps1') -Component V2rayN -Version '1.0.1' -RepositoryRoot $fork
    if ((Read-Dependencies $fork).v2rayN.tag -ne '1.0.1') { throw 'Source lock did not advance.' }
    if ((Get-Content -LiteralPath (Join-Path $fork 'README.md') -Raw) -notmatch 'Darfant') { throw 'Darfant README was overwritten.' }
    if ((Get-Content -LiteralPath (Join-Path $fork 'README.upstream.md') -Raw) -notmatch 'Upstream updated') { throw 'Upstream README was not updated.' }
    if ((Get-Content -LiteralPath (Join-Path $fork 'Shared.cs') -Raw) -notmatch 'darfant') { throw 'Customization was lost.' }
    if ((Get-Content -LiteralPath (Join-Path $fork 'Independent.cs') -Raw) -notmatch 'version = 2') { throw 'Upstream code was not integrated.' }
    $files = @(Invoke-DarfantGit $fork @('ls-files', '.github/workflows', '.github/dependabot.yml'))
    if ($files.Count -ne 1 -or $files[0] -ne '.github/workflows/darfant-windows.yml') { throw 'Upstream automation leaked into the fork.' }
    Commit-Fixture $fork

    # A real code conflict remains visible, then Continue records the resolved merge.
    Write-FixtureFile $upstream 'Shared.cs' "value = upstream2;`n"
    Commit-Fixture $upstream
    Invoke-DarfantGit $upstream @('tag', '1.0.2')
    $conflicted = $false
    try { & (Join-Path $scriptRoot 'Update-Darfant.ps1') -Component V2rayN -Version '1.0.2' -RepositoryRoot $fork }
    catch { $conflicted = $_.Exception.Message -like '*Resolve and stage*' }
    if (!$conflicted -or !(Invoke-DarfantGit $fork @('diff', '--name-only', '--diff-filter=U'))) { throw 'Code conflict was hidden.' }
    Write-FixtureFile $fork 'Shared.cs' "value = darfant_with_upstream2;`n"
    Invoke-DarfantGit $fork @('add', 'Shared.cs')
    & (Join-Path $scriptRoot 'Update-Darfant.ps1') -Component V2rayN -Continue -RepositoryRoot $fork
    Commit-Fixture $fork
    $latest = Read-Dependencies $fork
    Invoke-DarfantGit $fork @('merge-base', '--is-ancestor', $latest.v2rayN.commit, 'HEAD')

    # Another conflicting update can be aborted without losing the committed fork.
    Write-FixtureFile $upstream 'Shared.cs' "value = upstream3;`n"
    Commit-Fixture $upstream
    Invoke-DarfantGit $upstream @('tag', '1.0.3')
    $before = Invoke-DarfantGit $fork @('rev-parse', 'HEAD')
    try { & (Join-Path $scriptRoot 'Update-Darfant.ps1') -Component V2rayN -Version '1.0.3' -RepositoryRoot $fork } catch { }
    & (Join-Path $scriptRoot 'Update-Darfant.ps1') -Component V2rayN -Abort -RepositoryRoot $fork
    if ((Invoke-DarfantGit $fork @('rev-parse', 'HEAD')) -ne $before -or (Invoke-DarfantGit $fork @('status', '--porcelain'))) { throw 'Merge abort did not restore the fork.' }

    # Corrupt core/archive inputs must not reach a package.
    $badFile = Join-Path $temporary 'bad.zip'
    [IO.File]::WriteAllText($badFile, 'corrupt')
    $rejected = $false
    try { Assert-Sha256 $badFile ('0' * 64) } catch { $rejected = $_.Exception.Message -like '*verification failed*' }
    if (!$rejected) { throw 'A corrupt archive was accepted.' }
    Write-Host 'Maintenance integration checks passed: clean merge, branding/CI preservation, dirty tree, conflict/continue, abort, checksum.'
}
finally { Remove-DarfantTemporaryDirectory $temporary }
