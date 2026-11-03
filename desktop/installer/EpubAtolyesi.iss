#ifndef AppVersion
#define AppVersion "0.0.3"
#endif

#define AppName "EPUB Atölyesi"
#define PublishDir "build\publish"
#define WebView2Bootstrapper "build\prerequisites\MicrosoftEdgeWebView2Setup.exe"

[Setup]
AppId={{C5EC33E9-6938-4B59-A48C-8BA86376D490}
AppName={#AppName}
AppVersion={#AppVersion}
AppPublisher=nthreext-bit
DefaultDirName={autopf}\EPUB Atölyesi
DefaultGroupName={#AppName}
DisableProgramGroupPage=yes
UninstallDisplayName={#AppName}
UninstallDisplayIcon={app}\EpubAtolyesi.exe
OutputDir=dist
OutputBaseFilename=EpubAtolyesi-Setup-x64-v{#AppVersion}
ArchitecturesAllowed=x64
ArchitecturesInstallIn64BitMode=x64
PrivilegesRequired=admin
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
CloseApplications=yes

[Tasks]
Name: "desktopicon"; Description: "Masaüstü kısayolu oluştur"; GroupDescription: "Ek görevler:"; Flags: unchecked

[Files]
Source: "{#PublishDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs uninsrestartdelete uninsremovereadonly
Source: "{#WebView2Bootstrapper}"; Flags: dontcopy

[UninstallDelete]
Type: filesandordirs; Name: "{app}\*"

[Icons]
Name: "{autoprograms}\{#AppName}"; Filename: "{app}\EpubAtolyesi.exe"; WorkingDir: "{app}"
Name: "{autodesktop}\{#AppName}"; Filename: "{app}\EpubAtolyesi.exe"; WorkingDir: "{app}"; Tasks: desktopicon

[Run]
Filename: "{app}\EpubAtolyesi.exe"; Description: "{cm:LaunchProgram,{#AppName}}"; Flags: nowait postinstall skipifsilent

[Code]
const
  WebView2RuntimeClientKey = 'Software\Microsoft\EdgeUpdate\Clients\{F3017226-FE2A-4295-8BDF-00C3A9A7E4C5}';

var
  RemoveUserDataOnUninstall: Boolean;

function InitializeUninstall(): Boolean;
begin
  Result := True;
  RemoveUserDataOnUninstall :=
    MsgBox(
      'Uygulama ayarları, günlükler, kurtarma kopyaları ve WebView2 profili de silinsin mi? EPUB kitap dosyalarınız silinmez.',
      mbConfirmation, MB_YESNO) = IDYES;
end;

procedure CurUninstallStepChanged(CurUninstallStep: TUninstallStep);
var
  RoamingDataPath: string;
  LocalDataPath: string;
begin
  if (CurUninstallStep = usPostUninstall) and RemoveUserDataOnUninstall then
  begin
    RoamingDataPath := ExpandConstant('{userappdata}\EpubAtolyesi');
    LocalDataPath := ExpandConstant('{localappdata}\EpubAtolyesi');
    if DirExists(RoamingDataPath) and not DelTree(RoamingDataPath, True, True, True) then
      MsgBox('Kullanıcı ayarları, günlükler veya kurtarma kopyaları silinemedi: ' + RoamingDataPath,
        mbError, MB_OK);
    if DirExists(LocalDataPath) and not DelTree(LocalDataPath, True, True, True) then
      MsgBox('WebView2 kullanıcı verileri silinemedi: ' + LocalDataPath, mbError, MB_OK);
  end;
end;

function IsWebView2RuntimeInstalled: Boolean;
var
  Version: string;
begin
  Result :=
    (RegQueryStringValue(HKLM32, WebView2RuntimeClientKey, 'pv', Version) and
      (Version <> '') and (Version <> '0.0.0.0')) or
    (RegQueryStringValue(HKLM64, WebView2RuntimeClientKey, 'pv', Version) and
      (Version <> '') and (Version <> '0.0.0.0')) or
    (RegQueryStringValue(HKCU32, WebView2RuntimeClientKey, 'pv', Version) and
      (Version <> '') and (Version <> '0.0.0.0')) or
    (RegQueryStringValue(HKCU64, WebView2RuntimeClientKey, 'pv', Version) and
      (Version <> '') and (Version <> '0.0.0.0'));
end;

function PrepareToInstall(var NeedsRestart: Boolean): String;
var
  ExitCode: Integer;
begin
  Result := '';
  if IsWebView2RuntimeInstalled then
    Exit;

  ExtractTemporaryFile('MicrosoftEdgeWebView2Setup.exe');
  if not Exec(ExpandConstant('{tmp}\MicrosoftEdgeWebView2Setup.exe'),
      '/silent /install', '', SW_HIDE, ewWaitUntilTerminated, ExitCode) then
  begin
    Result := 'Microsoft Edge WebView2 Runtime yükleyicisi başlatılamadı.';
    Exit;
  end;

  if not IsWebView2RuntimeInstalled then
    Result := 'WebView2 Runtime yüklenemedi. İnternet bağlantınızı kontrol edip kurulumu yeniden çalıştırın.';
end;
