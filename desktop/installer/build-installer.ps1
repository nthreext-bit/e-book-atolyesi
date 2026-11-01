param(
    [string]$InnoSetupCompiler,
    [string]$AppVersion = '0.0.4',
    [switch]$RequireSigning,
    [switch]$SelfSignedSigning
)

$ErrorActionPreference = 'Stop'

$installerDirectory = $PSScriptRoot
$repositoryDirectory = (Resolve-Path (Join-Path $installerDirectory '..\..')).Path
$projectFile = Join-Path $repositoryDirectory 'desktop\EpubStudio.csproj'
$publishDirectory = Join-Path $installerDirectory 'build\publish'
$prerequisiteDirectory = Join-Path $installerDirectory 'build\prerequisites'
$bootstrapperPath = Join-Path $prerequisiteDirectory 'MicrosoftEdgeWebView2Setup.exe'
$signingCertificatePath = Join-Path $prerequisiteDirectory 'CodeSigningCertificate.pfx'
$outputDirectory = Join-Path $installerDirectory 'dist'

New-Item -ItemType Directory -Force -Path $prerequisiteDirectory, $outputDirectory | Out-Null

$certificateBase64 = $env:WINDOWS_SIGNING_CERTIFICATE_BASE64
$certificatePassword = $env:WINDOWS_SIGNING_PASSWORD
$hasSigningCertificate = -not [string]::IsNullOrWhiteSpace($certificateBase64)
$hasSigningPassword = -not [string]::IsNullOrWhiteSpace($certificatePassword)
$selfSignedCertificate = $null
$signingThumbprint = $null
$temporaryTrustInstalled = $false
if ($hasSigningCertificate -ne $hasSigningPassword) {
    throw 'Set both WINDOWS_SIGNING_CERTIFICATE_BASE64 and WINDOWS_SIGNING_PASSWORD to sign the installer.'
}
if ($hasSigningCertificate -and $SelfSignedSigning) {
    throw 'Choose either a configured code-signing certificate or self-signed signing, not both.'
}
if ($RequireSigning -and -not $hasSigningCertificate -and -not $SelfSignedSigning) {
    throw 'Signing is required. Configure the code-signing certificate secrets or enable self-signed signing.'
}

$signTool = $null
if ($hasSigningCertificate -or $SelfSignedSigning) {
    $signToolCommand = Get-Command 'signtool.exe' -ErrorAction SilentlyContinue
    if ($signToolCommand) {
        $signTool = $signToolCommand.Source
    }
    else {
        $windowsKitDirectory = Join-Path ${env:ProgramFiles(x86)} 'Windows Kits\10\bin'
        $signTool = Get-ChildItem $windowsKitDirectory -Filter 'signtool.exe' -Recurse -ErrorAction Stop |
            Where-Object { $_.FullName -match '\\x64\\signtool\.exe$' } |
            Sort-Object FullName -Descending |
            Select-Object -ExpandProperty FullName -First 1
    }
    if ([string]::IsNullOrWhiteSpace($signTool)) {
        throw 'Windows SDK signtool.exe was not found.'
    }
    if ($SelfSignedSigning) {
        $selfSignedCertificate = New-SelfSignedCertificate `
            -Type CodeSigningCert `
            -Subject 'CN=EPUB Atölyesi (Self-Signed)' `
            -CertStoreLocation 'Cert:\CurrentUser\My' `
            -KeyAlgorithm RSA `
            -KeyLength 3072 `
            -HashAlgorithm SHA256 `
            -NotAfter (Get-Date).AddYears(2)
        $signingThumbprint = $selfSignedCertificate.Thumbprint
        $publicCertificatePath = Join-Path $outputDirectory "EpubAtolyesi-SelfSigned-v$AppVersion.cer"
        Export-Certificate -Cert $selfSignedCertificate -FilePath $publicCertificatePath | Out-Null
        Import-Certificate -FilePath $publicCertificatePath -CertStoreLocation 'Cert:\CurrentUser\Root' | Out-Null
        $temporaryTrustInstalled = $true
    }
    elseif ($hasSigningCertificate) {
        [System.IO.File]::WriteAllBytes(
            $signingCertificatePath,
            [Convert]::FromBase64String($certificateBase64))
    }
}

function Sign-Executable([string]$Path) {
    if ($signingThumbprint) {
        & $signTool sign `
            /s My `
            /sha1 $signingThumbprint `
            /fd SHA256 `
            /tr 'http://timestamp.digicert.com' `
            /td SHA256 `
            $Path
    }
    else {
        & $signTool sign `
            /f $signingCertificatePath `
            /p $certificatePassword `
            /fd SHA256 `
            /tr 'http://timestamp.digicert.com' `
            /td SHA256 `
            $Path
    }
    if ($LASTEXITCODE -ne 0) {
        throw "Authenticode signing failed for $Path with exit code $LASTEXITCODE."
    }

    $signature = Get-AuthenticodeSignature -FilePath $Path
    if ($signature.Status -ne [System.Management.Automation.SignatureStatus]::Valid) {
        throw "Authenticode signature verification failed for ${Path}: $($signature.Status)."
    }
}

try {
& dotnet publish $projectFile `
    --configuration Release `
    --runtime win-x64 `
    --self-contained true `
    --output $publishDirectory `
    -p:PublishSingleFile=false `
    -p:IncludeNativeLibrariesForSelfExtract=false
if ($LASTEXITCODE -ne 0) {
    throw "dotnet publish failed with exit code $LASTEXITCODE."
}

if ($hasSigningCertificate) {
    Sign-Executable (Join-Path $publishDirectory 'EpubAtolyesi.exe')
}

Invoke-WebRequest `
    -Uri 'https://go.microsoft.com/fwlink/p/?LinkId=2124703' `
    -OutFile $bootstrapperPath

$signature = Get-AuthenticodeSignature -FilePath $bootstrapperPath
if ($signature.Status -ne [System.Management.Automation.SignatureStatus]::Valid -or
    $signature.SignerCertificate.Subject -notlike '*Microsoft*') {
    throw 'The downloaded WebView2 bootstrapper does not have a valid Microsoft signature.'
}

if ([string]::IsNullOrWhiteSpace($InnoSetupCompiler)) {
    $compilerCommand = Get-Command 'ISCC.exe' -ErrorAction SilentlyContinue
    if ($compilerCommand) {
        $InnoSetupCompiler = $compilerCommand.Source
    }
    else {
        $candidates = @(
            (Join-Path ${env:ProgramFiles(x86)} 'Inno Setup 6\ISCC.exe'),
            (Join-Path $env:ProgramFiles 'Inno Setup 6\ISCC.exe')
        )
        $InnoSetupCompiler = $candidates | Where-Object { Test-Path $_ } | Select-Object -First 1
    }
}

if ([string]::IsNullOrWhiteSpace($InnoSetupCompiler) -or
    -not (Test-Path $InnoSetupCompiler)) {
    throw 'Inno Setup 6 was not found. Install it or pass its ISCC.exe path with -InnoSetupCompiler.'
}

& $InnoSetupCompiler "/DAppVersion=$AppVersion" (Join-Path $installerDirectory 'EpubAtolyesi.iss')
if ($LASTEXITCODE -ne 0) {
    throw "Inno Setup compilation failed with exit code $LASTEXITCODE."
}

$installerPath = Join-Path $outputDirectory "EpubAtolyesi-Setup-x64-v$AppVersion.exe"
if (-not (Test-Path $installerPath)) {
    throw "Expected installer was not created: $installerPath"
}

if ($hasSigningCertificate) {
    Sign-Executable $installerPath
}

Write-Output "Installer created: $installerPath"
}
finally {
    if (Test-Path $signingCertificatePath) {
        Remove-Item -LiteralPath $signingCertificatePath -Force
    }
    if ($temporaryTrustInstalled) {
        Remove-Item "Cert:\CurrentUser\Root\$signingThumbprint" -Force
    }
    if ($selfSignedCertificate) {
        Remove-Item "Cert:\CurrentUser\My\$signingThumbprint" -Force
    }
}
