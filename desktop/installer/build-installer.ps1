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
$signingKeyPath = Join-Path $prerequisiteDirectory 'CodeSigningPrivateKey.pem'
$signingPemPath = Join-Path $prerequisiteDirectory 'CodeSigningCertificate.pem'
$outputDirectory = Join-Path $installerDirectory 'dist'

New-Item -ItemType Directory -Force -Path $prerequisiteDirectory, $outputDirectory | Out-Null

try {
$certificateBase64 = $env:WINDOWS_SIGNING_CERTIFICATE_BASE64
$certificatePassword = $env:WINDOWS_SIGNING_PASSWORD
$hasSigningCertificate = -not [string]::IsNullOrWhiteSpace($certificateBase64)
$hasSigningPassword = -not [string]::IsNullOrWhiteSpace($certificatePassword)
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
$selfSignedThumbprint = $null
if ($hasSigningCertificate -or $SelfSignedSigning) {
    $signToolCommand = Get-Command 'signtool.exe' -ErrorAction SilentlyContinue
    if ($signToolCommand) {
        $signTool = $signToolCommand.Source
    }
    else {
        Write-Output 'Locating Windows SDK signtool.exe...'
        $windowsKitDirectory = Join-Path ${env:ProgramFiles(x86)} 'Windows Kits\10\bin'
        $signTool = Get-ChildItem $windowsKitDirectory -Directory -ErrorAction Stop |
            Sort-Object Name -Descending |
            ForEach-Object { Join-Path $_.FullName 'x64\signtool.exe' } |
            Where-Object { Test-Path $_ } |
            Select-Object -First 1
    }
    if ([string]::IsNullOrWhiteSpace($signTool)) {
        throw 'Windows SDK signtool.exe was not found.'
    }
    if ($SelfSignedSigning) {
        Write-Output 'Creating temporary self-signed code-signing certificate with OpenSSL...'
        $openSslCommand = Get-Command 'openssl.exe' -ErrorAction SilentlyContinue
        if ($openSslCommand) {
            $openSsl = $openSslCommand.Source
        }
        else {
            $openSsl = Join-Path $env:ProgramFiles 'Git\usr\bin\openssl.exe'
        }
        if (-not (Test-Path $openSsl)) {
            throw 'OpenSSL was not found on the Windows runner.'
        }

        $certificatePassword = [Guid]::NewGuid().ToString('N') + [Guid]::NewGuid().ToString('N')
        & $openSsl req -x509 -newkey rsa:3072 -nodes `
            -keyout $signingKeyPath `
            -out $signingPemPath `
            -days 730 `
            -subj '/CN=EPUB Atölyesi (Self-Signed)' `
            -addext 'keyUsage=digitalSignature' `
            -addext 'extendedKeyUsage=codeSigning'
        if ($LASTEXITCODE -ne 0) {
            throw "OpenSSL certificate creation failed with exit code $LASTEXITCODE."
        }
        Write-Output 'Temporary self-signed certificate created.'

        & $openSsl pkcs12 -export `
            -out $signingCertificatePath `
            -inkey $signingKeyPath `
            -in $signingPemPath `
            -passout "pass:$certificatePassword"
        if ($LASTEXITCODE -ne 0) {
            throw "OpenSSL PFX creation failed with exit code $LASTEXITCODE."
        }
        Write-Output 'Temporary signing key exported to PFX.'

        $publicCertificatePath = Join-Path $outputDirectory "EpubAtolyesi-SelfSigned-v$AppVersion.cer"
        & $openSsl x509 -in $signingPemPath -outform DER -out $publicCertificatePath
        if ($LASTEXITCODE -ne 0) {
            throw "OpenSSL public certificate export failed with exit code $LASTEXITCODE."
        }
        $publicCertificate = [System.Security.Cryptography.X509Certificates.X509Certificate2]::new($publicCertificatePath)
        $selfSignedThumbprint = $publicCertificate.Thumbprint
        $publicCertificate.Dispose()
        Write-Output "Public signing certificate exported: $selfSignedThumbprint"
    }
    elseif ($hasSigningCertificate) {
        [System.IO.File]::WriteAllBytes(
            $signingCertificatePath,
            [Convert]::FromBase64String($certificateBase64))
    }
}

function Sign-Executable([string]$Path) {
    $signArguments = @('sign', '/f', $signingCertificatePath, '/p', $certificatePassword, '/fd', 'SHA256')
    if (-not $SelfSignedSigning) {
        $signArguments += @('/tr', 'http://timestamp.digicert.com', '/td', 'SHA256')
    }
    $signArguments += $Path
    & $signTool @signArguments
    if ($LASTEXITCODE -ne 0) {
        throw "Authenticode signing failed for $Path with exit code $LASTEXITCODE."
    }

    $signature = Get-AuthenticodeSignature -FilePath $Path
    $isExpectedSelfSigned = $SelfSignedSigning -and
        $signature.Status -eq [System.Management.Automation.SignatureStatus]::NotTrusted -and
        $signature.SignerCertificate -and
        $signature.SignerCertificate.Thumbprint -eq $selfSignedThumbprint
    if ($signature.Status -ne [System.Management.Automation.SignatureStatus]::Valid -and
        -not $isExpectedSelfSigned) {
        throw "Authenticode signature verification failed for ${Path}: $($signature.Status)."
    }
}

Write-Output 'Publishing the self-contained Windows application...'
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
Write-Output 'Application publish completed.'

if ($hasSigningCertificate -or $SelfSignedSigning) {
    Write-Output 'Signing the application executable...'
    Sign-Executable (Join-Path $publishDirectory 'EpubAtolyesi.exe')
}

Write-Output 'Downloading the Microsoft WebView2 bootstrapper...'
Invoke-WebRequest `
    -Uri 'https://go.microsoft.com/fwlink/p/?LinkId=2124703' `
    -OutFile $bootstrapperPath `
    -TimeoutSec 120
Write-Output 'WebView2 bootstrapper download completed.'

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

Write-Output 'Compiling the Windows installer...'
& $InnoSetupCompiler "/DAppVersion=$AppVersion" (Join-Path $installerDirectory 'EpubAtolyesi.iss')
if ($LASTEXITCODE -ne 0) {
    throw "Inno Setup compilation failed with exit code $LASTEXITCODE."
}

$installerPath = Join-Path $outputDirectory "EpubAtolyesi-Setup-x64-v$AppVersion.exe"
if (-not (Test-Path $installerPath)) {
    throw "Expected installer was not created: $installerPath"
}

if ($hasSigningCertificate -or $SelfSignedSigning) {
    Write-Output 'Signing the installer executable...'
    Sign-Executable $installerPath
}

Write-Output "Installer created: $installerPath"
}
finally {
    foreach ($temporaryFile in @($signingCertificatePath, $signingKeyPath, $signingPemPath)) {
        if (Test-Path $temporaryFile) {
            Remove-Item -LiteralPath $temporaryFile -Force
        }
    }
}
