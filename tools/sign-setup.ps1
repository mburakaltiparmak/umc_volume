# One-time setup: creates a self-signed code-signing certificate for the given name,
# exports its public part to cert\, and writes the name into src\AssemblyInfo.cs.
# Usage: powershell -NoProfile -ExecutionPolicy Bypass -File tools\sign-setup.ps1 -Name "Full Name"
param(
    [Parameter(Mandatory = $true)][string]$Name,
    [switch]$Force
)
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$certDir = Join-Path $root 'cert'
$thumbFile = Join-Path $certDir 'thumbprint.txt'

if ((Test-Path $thumbFile) -and -not $Force) {
    throw "A signing certificate is already set up ($thumbFile). Use -Force to create a new one."
}
New-Item -ItemType Directory -Force $certDir | Out-Null

# Quote the CN when the name contains X.500 special characters.
$cn = if ($Name -match '[,+=<>#;"\\]') { 'CN="' + ($Name -replace '"', '\"') + '"' } else { "CN=$Name" }

$cert = New-SelfSignedCertificate -Type CodeSigningCert -Subject $cn `
    -CertStoreLocation Cert:\CurrentUser\My -KeyAlgorithm RSA -KeyLength 3072 `
    -HashAlgorithm SHA256 -KeyExportPolicy Exportable -NotAfter (Get-Date).AddYears(5) `
    -FriendlyName 'UmcVolume code signing'

Export-Certificate -Cert $cert -FilePath (Join-Path $certDir 'UmcVolume-signing.cer') | Out-Null
[IO.File]::WriteAllText($thumbFile, $cert.Thumbprint)

# Put the publisher name into the assembly metadata (file Properties > Details).
$asm = Join-Path $root 'src\AssemblyInfo.cs'
$text = [IO.File]::ReadAllText($asm)
$esc = $Name -replace '\\', '\\' -replace '"', '\"'
$year = (Get-Date).Year
$copy = [char]0xA9  # (c) sign; kept out of the source so Windows PowerShell 5.1 reads this file correctly
$text = $text -replace '\[assembly: AssemblyCopyright\("[^"]*"\)\]', "[assembly: AssemblyCopyright(""$copy $year $esc"")]"
if ($text -match 'AssemblyCompany') {
    $text = $text -replace '\[assembly: AssemblyCompany\("[^"]*"\)\]', "[assembly: AssemblyCompany(""$esc"")]"
} else {
    $text = $text -replace '(\[assembly: AssemblyProduct\("[^"]*"\)\])', "`$1`r`n[assembly: AssemblyCompany(""$esc"")]"
}
[IO.File]::WriteAllText($asm, $text, (New-Object Text.UTF8Encoding $false))

Write-Host "Certificate : $($cert.Subject)"
Write-Host "Thumbprint  : $($cert.Thumbprint)"
Write-Host "Valid until : $($cert.NotAfter.ToString('yyyy-MM-dd'))"
Write-Host "Public cert : $certDir\UmcVolume-signing.cer"
Write-Host "Private key : Cert:\CurrentUser\My\$($cert.Thumbprint) (back it up; it never leaves this store)"
