# Signs a file with the certificate set up by tools\sign-setup.ps1 (SHA-256 + RFC 3161 timestamp).
# Usage: powershell -NoProfile -ExecutionPolicy Bypass -File tools\sign.ps1 -File UmcVolume.exe
param([Parameter(Mandatory = $true)][string]$File)
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$thumb = ([IO.File]::ReadAllText((Join-Path $root 'cert\thumbprint.txt'))).Trim()

if (-not (Test-Path "Cert:\CurrentUser\My\$thumb")) {
    throw "Signing certificate $thumb is not in Cert:\CurrentUser\My on this machine."
}

$signtool = Get-ChildItem "${env:ProgramFiles(x86)}\Windows Kits\10\bin\*\x64\signtool.exe" -ErrorAction SilentlyContinue |
    Sort-Object { [version]$_.Directory.Parent.Name } -Descending | Select-Object -First 1
if (-not $signtool) { throw 'signtool.exe not found (install the Windows SDK).' }

# A timestamp keeps the signature valid after the certificate expires.
# Only the file's hash is sent to the timestamp server.
& $signtool.FullName sign /q /sha1 $thumb /fd SHA256 /tr http://timestamp.digicert.com /td SHA256 $File
if ($LASTEXITCODE -ne 0) {
    Write-Warning 'Timestamp server unreachable; signing without a timestamp.'
    & $signtool.FullName sign /q /sha1 $thumb /fd SHA256 $File
    if ($LASTEXITCODE -ne 0) { throw 'signtool failed.' }
}
$sig = Get-AuthenticodeSignature $File
Write-Host "Signed: $File  [$($sig.SignerCertificate.Subject), $thumb]"
