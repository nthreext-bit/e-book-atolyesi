param(
    [string]$InnoSetupCompiler
)

$ErrorActionPreference = 'Stop'

$installerDirectory = $PSScriptRoot
$repositoryDirectory = (Resolve-Path (Join-Path $installerDirectory '..\..')).Path
$projectFile = Join-Path $repositoryDirectory 'desktop\EpubStudio.csproj'
$publishDirectory = Join-Path $installerDirectory 'build\publish'
$prerequisiteDirectory = Join-Path $installerDirectory 'build\prerequisites'
$bootstrapperPath = Join-Path $prerequisiteDirectory 'MicrosoftEdgeWebView2Setup.exe'
$outputDirectory = Join-Path $installerDirectory 'dist'

New-Item -ItemType Directory -Force -Path $prerequisiteDirectory, $outputDirectory | Out-Null

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

& $InnoSetupCompiler (Join-Path $installerDirectory 'EpubAtolyesi.iss')
if ($LASTEXITCODE -ne 0) {
    throw "Inno Setup compilation failed with exit code $LASTEXITCODE."
}

$installerPath = Join-Path $outputDirectory 'EpubAtolyesi-Setup-x64-v0.0.2.exe'
if (-not (Test-Path $installerPath)) {
    throw "Expected installer was not created: $installerPath"
}

Write-Output "Installer created: $installerPath"
