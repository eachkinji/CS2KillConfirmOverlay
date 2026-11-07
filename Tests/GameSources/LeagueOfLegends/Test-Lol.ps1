param([switch]$Ui, [string]$Service)
$ErrorActionPreference = 'Stop'
$repository = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../../..'))
$tls = Join-Path $repository 'Output/LoLIntegration/tls'
New-Item -ItemType Directory -Force -Path $tls | Out-Null
$key = [Security.Cryptography.RSA]::Create(2048)
try {
    $request = [Security.Cryptography.X509Certificates.CertificateRequest]::new('CN=localhost', $key,
        [Security.Cryptography.HashAlgorithmName]::SHA256, [Security.Cryptography.RSASignaturePadding]::Pkcs1)
    $certificate = $request.CreateSelfSigned([DateTimeOffset]::UtcNow.AddDays(-1), [DateTimeOffset]::UtcNow.AddDays(2))
    try {
        [IO.File]::WriteAllText((Join-Path $tls 'cert.pem'), $certificate.ExportCertificatePem())
        [IO.File]::WriteAllText((Join-Path $tls 'key.pem'), $key.ExportPkcs8PrivateKeyPem())
    } finally { $certificate.Dispose() }
} finally { $key.Dispose() }
$arguments = @((Join-Path $PSScriptRoot 'test_live_client.py'))
if ($Ui) { $arguments += '--ui' }
if ($Service) { $arguments += @('--service', [IO.Path]::GetFullPath($Service)) }
& python @arguments
if ($LASTEXITCODE -ne 0) { throw 'LOL isolated integration test failed.' }
