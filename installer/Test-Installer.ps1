param([Parameter(Mandatory)][string] $Installer, [Parameter(Mandatory)][string] $Package)
. (Join-Path (Split-Path $PSScriptRoot) 'maintenance/Common.ps1')
$Installer = (Resolve-Path -LiteralPath $Installer).Path
$Package = (Resolve-Path -LiteralPath $Package).Path
$registration = 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Uninstall\{7E56FFAE-074B-4ED6-AFF9-7E1E35DBE5A7}_is1'
$links = @(
    (Join-Path ([Environment]::GetFolderPath('Desktop')) 'دارفانت.lnk'),
    (Join-Path ([Environment]::GetFolderPath('Programs')) 'دارفانت.lnk')
)
# Never replace a real installation or an existing user shortcut with a test.
if (Test-Path -LiteralPath $registration) { throw 'Use a clean Windows profile for installer QA: Darfant is already registered.' }
foreach ($link in $links) { if (Test-Path -LiteralPath $link) { throw "Installer QA would replace an existing shortcut: $link" } }
$temporary = Join-Path ([IO.Path]::GetTempPath()) ('Darfant-' + [Guid]::NewGuid().ToString('N'))
$installed = Join-Path $temporary 'Installed Darfant'
$uninstaller = Join-Path $installed 'unins000.exe'
New-Item -ItemType Directory -Path $temporary | Out-Null
function Invoke-TestSetup {
    $arguments = @('/VERYSILENT', '/SUPPRESSMSGBOXES', '/NORESTART', '/SP-', ('/DIR="' + $installed + '"'), ('/LOG="' + (Join-Path $temporary 'setup.log') + '"'))
    $process = Start-Process -FilePath $Installer -ArgumentList $arguments -WindowStyle Hidden -Wait -PassThru
    if ($process.ExitCode -ne 0) { throw "Installer QA failed with exit code $($process.ExitCode)." }
}
function Invoke-TestUninstall {
    $process = Start-Process -FilePath $uninstaller -ArgumentList @('/VERYSILENT', '/SUPPRESSMSGBOXES', '/NORESTART') -WindowStyle Hidden -Wait -PassThru
    if ($process.ExitCode -ne 0) { throw "Uninstaller QA failed with exit code $($process.ExitCode)." }
}
try {
    Invoke-TestSetup
    foreach ($source in Get-ChildItem -LiteralPath $Package -Recurse -File) {
        $relative = $source.FullName.Substring($Package.TrimEnd('\').Length + 1)
        $destination = Join-Path $installed $relative
        if (!(Test-Path -LiteralPath $destination)) { throw "Installer omitted $relative" }
        Assert-Sha256 $destination (Get-FileHash -LiteralPath $source.FullName -Algorithm SHA256).Hash
    }
    $shell = New-Object -ComObject WScript.Shell
    foreach ($link in $links) {
        if (!(Test-Path -LiteralPath $link)) { throw "Missing shortcut: $link" }
        $shortcut = $shell.CreateShortcut($link)
        if ($shortcut.TargetPath -ne (Join-Path $installed 'Darfant.exe') -or $shortcut.WorkingDirectory -ne $installed) { throw 'The shortcut opens the wrong program or folder.' }
    }
    $entry = Get-ItemProperty -LiteralPath $registration
    $version = (Get-Item -LiteralPath (Join-Path $Package 'Darfant.exe')).VersionInfo.ProductVersion.Split('+')[0]
    if ($entry.DisplayVersion -ne $version) { throw 'Windows uninstall registration has the wrong version.' }
    $settings = Join-Path $installed 'guiConfigs'
    New-Item -ItemType Directory -Path $settings | Out-Null
    $marker = Join-Path $settings 'installer-qa-marker.txt'
    [IO.File]::WriteAllText($marker, 'Preserve user settings')
    Invoke-TestSetup
    if ((Get-Content -LiteralPath $marker -Raw) -ne 'Preserve user settings') { throw 'Reinstall overwrote user data.' }
    Invoke-TestUninstall
    if (Test-Path -LiteralPath (Join-Path $installed 'Darfant.exe')) { throw 'Uninstall did not remove Darfant.exe.' }
    if (Test-Path -LiteralPath $registration) { throw 'Uninstall registration was left behind.' }
    foreach ($link in $links) { if (Test-Path -LiteralPath $link) { throw 'Uninstall left a broken shortcut.' } }
    if (!(Test-Path -LiteralPath $marker)) { throw 'Uninstall deleted the user settings.' }
    Write-Host 'Installer QA passed: full payload, desktop/Start shortcuts, reinstall, Windows registration, uninstall, settings preservation.'
}
finally {
    if (Test-Path -LiteralPath $uninstaller) { Invoke-TestUninstall }
    Remove-DarfantTemporaryDirectory $temporary
}
