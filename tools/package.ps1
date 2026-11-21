# Builds the hand-off package: dist\UmcVolume-<version>.zip with the signed exe, full source,
# the public signing certificate and SHA256SUMS.txt.
# Usage: powershell -NoProfile -ExecutionPolicy Bypass -File tools\package.ps1
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot

& cmd /c "`"$root\build.cmd`""
if ($LASTEXITCODE -ne 0) { throw 'Build failed.' }

$exe = Join-Path $root 'UmcVolume.exe'
$version = (Get-Item $exe).VersionInfo.ProductVersion
$name = "UmcVolume-$version"
$dist = Join-Path $root 'dist'
$stage = Join-Path $dist $name
if (Test-Path $stage) { Remove-Item -Recurse -Force $stage }
New-Item -ItemType Directory -Force $stage | Out-Null

foreach ($item in 'UmcVolume.exe', 'README.md', 'README.en.md', 'LICENSE', 'app.manifest', 'build.cmd', 'build-test.cmd', 'src', 'tools', 'assets') {
    Copy-Item -Recurse (Join-Path $root $item) $stage
}
$cer = Join-Path $root 'cert\UmcVolume-signing.cer'
if (Test-Path $cer) {
    New-Item -ItemType Directory -Force (Join-Path $stage 'cert') | Out-Null
    Copy-Item $cer (Join-Path $stage 'cert')
}

# Hash list (sha256sum format, forward slashes) for every file in the package.
$lines = Get-ChildItem -Recurse -File $stage | Sort-Object FullName | ForEach-Object {
    $rel = $_.FullName.Substring($stage.Length + 1) -replace '\\', '/'
    '{0}  {1}' -f (Get-FileHash $_.FullName -Algorithm SHA256).Hash.ToLower(), $rel
}
[IO.File]::WriteAllLines((Join-Path $stage 'SHA256SUMS.txt'), $lines)

$zip = Join-Path $dist "$name.zip"
if (Test-Path $zip) { Remove-Item -Force $zip }
Compress-Archive -Path $stage -DestinationPath $zip

$sig = Get-AuthenticodeSignature $exe
Write-Host ''
Write-Host "Package     : $zip"
Write-Host "ZIP SHA-256 : $((Get-FileHash $zip -Algorithm SHA256).Hash.ToLower())"
Write-Host "EXE SHA-256 : $((Get-FileHash $exe -Algorithm SHA256).Hash.ToLower())"
if ($sig.SignerCertificate) {
    Write-Host "Signer      : $($sig.SignerCertificate.Subject)"
    Write-Host "Thumbprint  : $($sig.SignerCertificate.Thumbprint)"
} else {
    Write-Warning 'UmcVolume.exe is not signed (run tools\sign-setup.ps1 first).'
}
