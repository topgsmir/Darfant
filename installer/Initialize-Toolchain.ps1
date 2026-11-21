param([Parameter(Mandatory)][string] $Output)
. (Join-Path (Split-Path $PSScriptRoot) 'maintenance/Common.ps1')
$tool = Get-Content -LiteralPath (Join-Path $PSScriptRoot 'toolchain.json') -Raw | ConvertFrom-Json
$Output = $ExecutionContext.SessionState.Path.GetUnresolvedProviderPathFromPSPath($Output)
$compiler = Join-Path $Output 'ISCC.exe'
if (Test-Path -LiteralPath $compiler) {
    $installedVersion = & $compiler --version
    if ($LASTEXITCODE -ne 0 -or $installedVersion -ne $tool.version) { throw 'An unexpected compiler version is installed in the requested output folder.' }
    return $compiler
}
$download = Join-Path ([IO.Path]::GetTempPath()) ('Darfant-' + [Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $download | Out-Null
try {
    $setup = Join-Path $download 'innosetup.exe'
    & curl.exe --fail --location --retry 2 --output $setup $tool.url
    if ($LASTEXITCODE -ne 0) { throw 'Inno Setup download failed.' }
    Assert-Sha256 $setup $tool.sha256
    $signature = Get-AuthenticodeSignature -LiteralPath $setup
    if ($signature.Status -ne 'Valid' -or $signature.SignerCertificate.Subject -notmatch 'CN=Pyrsys B\.V\.') {
        throw 'The official Inno Setup installer signature could not be verified.'
    }
    $arguments = @('/VERYSILENT', '/SUPPRESSMSGBOXES', '/NORESTART', '/CURRENTUSER', '/NOICONS', ('/DIR="' + $Output + '"'))
    $install = Start-Process -FilePath $setup -ArgumentList $arguments -WindowStyle Hidden -Wait -PassThru
    if ($install.ExitCode -ne 0 -or !(Test-Path -LiteralPath $compiler)) { throw 'Inno Setup compiler installation failed.' }
    return $compiler
}
finally { Remove-DarfantTemporaryDirectory $download }
