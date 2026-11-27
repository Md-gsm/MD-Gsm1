#ifndef PublishDir
  #define PublishDir AddBackslash(SourcePath) + "..\artifacts\windows-publish\desktop"
#endif
#ifndef ApiPublishDir
  #define ApiPublishDir AddBackslash(SourcePath) + "..\artifacts\windows-publish\api"
#endif
#ifndef OutputDir
  #define OutputDir AddBackslash(SourcePath) + "..\artifacts\installer"
#endif

#define AppName "نرم‌افزار جامع مدیریت و حسابداری تعمیرگاه موبایل"
#define AppVersion "0.3.0"
#define AppPublisher "MD-Gsm"
#define AppExeName "MDGsm.Desktop.exe"

[Setup]
AppId={{D64C41D7-3B82-4B64-94F9-5FA4FCD9E130}
AppName={#AppName}
AppVersion={#AppVersion}
AppPublisher={#AppPublisher}
AppComments=Controlled Windows installation candidate. API service uses LocalService and a service-specific SID. Persons promotion remains feature-gated; no database migration is executed.
DefaultDirName={autopf}\MD-Gsm
DisableDirPage=yes
DefaultGroupName={#AppName}
DisableProgramGroupPage=yes
PrivilegesRequired=admin
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
OutputDir={#OutputDir}
OutputBaseFilename=MD-Gsm-Setup-v0.3.0
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
UninstallDisplayName={#AppName}
Uninstallable=yes
CloseApplications=yes
RestartApplications=no
SetupLogging=yes
UsePreviousAppDir=no

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "Create a desktop shortcut"; GroupDescription: "Additional shortcuts:"; Flags: unchecked

[Files]
Source: "{#PublishDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs
Source: "{#ApiPublishDir}\*"; DestDir: "{app}\Api"; Flags: ignoreversion recursesubdirs createallsubdirs
Source: "{#SourcePath}\Install-MDGsmApiService.ps1"; DestDir: "{app}"; Flags: ignoreversion
Source: "{#SourcePath}\Provision-MDGsmCentralDatabase.ps1"; DestDir: "{app}"; Flags: ignoreversion
Source: "{#SourcePath}\Configure-MDGsmNetwork.ps1"; DestDir: "{app}"; Flags: ignoreversion
Source: "{#SourcePath}\Initialize-MDGsmCertificateProvider.ps1"; DestDir: "{app}"; Flags: ignoreversion
Source: "{#SourcePath}\Install-MDGsmApiService.ps1"; Flags: dontcopy
Source: "{#SourcePath}\Uninstall-MDGsmApiService.ps1"; DestDir: "{app}"; Flags: ignoreversion

[Icons]
Name: "{group}\نرم‌افزار جامع مدیریت و حسابداری تعمیرگاه موبایل"; Filename: "{app}\{#AppExeName}"
Name: "{autodesktop}\نرم‌افزار جامع مدیریت و حسابداری تعمیرگاه موبایل"; Filename: "{app}\{#AppExeName}"; Tasks: desktopicon

[Code]
var
  ServiceStatePath: String;
  ServiceSetupScriptPath: String;
  ServiceSetupLogPath: String;

function InitializeSetup(): Boolean;
begin
  ServiceStatePath := ExpandConstant('{tmp}\MD-Gsm-MDGsmApi-ServiceState.json');
  ServiceSetupScriptPath := ExpandConstant('{tmp}\Install-MDGsmApiService.ps1');
  ExtractTemporaryFile('Install-MDGsmApiService.ps1');
  Result := True;
end;

procedure RunApiServicePhase(const Phase: String);
var
  ResultCode: Integer;
  PowerShellPath: String;
  Parameters: String;
begin
  PowerShellPath := ExpandConstant('{sys}\WindowsPowerShell\v1.0\powershell.exe');
  Parameters := '-NoProfile -NonInteractive -ExecutionPolicy Bypass -File "' +
    ServiceSetupScriptPath + '" -InstallDirectory "' + ExpandConstant('{app}') +
    '" -ServiceStatePath "' + ServiceStatePath + '" -SetupLogPath "' + ServiceSetupLogPath + '" -Phase "' + Phase + '"';
  if not Exec(PowerShellPath, Parameters, ExpandConstant('{tmp}'), SW_HIDE, ewWaitUntilTerminated, ResultCode) then
    RaiseException('Could not start the MD-Gsm API service setup phase: ' + Phase);
  if ResultCode <> 0 then
  begin
    if LoadStringFromFile(ServiceSetupLogPath, NetworkConfigText) then
      RaiseException('MD-Gsm API service setup phase failed: ' + Phase + ' (exit ' + IntToStr(ResultCode) + ').' + #13#10 +
        Copy(String(NetworkConfigText), 1, 12000))
    else
      RaiseException('MD-Gsm API service setup phase failed: ' + Phase + ' (exit ' + IntToStr(ResultCode) + '); setup diagnostic log was not created: ' + ServiceSetupLogPath);
  end;
end;

function PrepareToInstall(var NeedsRestart: Boolean): String;
begin
  // Runs before Inno Setup checks/terminates processes using files to be replaced.
  // Stop MDGsmApi deliberately here, not through implicit Restart Manager behavior.
  RunApiServicePhase('Prepare');
  Result := '';
end;

procedure CurStepChanged(CurStep: TSetupStep);
var
  NetworkConfigText: AnsiString;
  NetworkConfigPath: String;
begin
  if CurStep = ssPostInstall then
  begin
    RunApiServicePhase('Install');
    if not WizardSilent then
    begin
      NetworkConfigPath := ExpandConstant('{commonappdata}\MD-Gsm\Network\api-network.json');
      if LoadStringFromFile(NetworkConfigPath, NetworkConfigText) and
         (Pos('"RemoteAccessEnabled": true', NetworkConfigText) > 0) then
      begin
        MsgBox(
          'نصب سرور مرکزی و HTTPS انجام شد.' + #13#10 + #13#10 +
          'برای اتصال رایانه‌های دیگر، فایل اتصال زیر را از طریق فلش مورد اعتماد یا مسیر امن به آن رایانه منتقل کنید و در نصب‌کنندهٔ Client انتخاب کنید:' + #13#10 +
          'C:\Users\Public\Documents\MD-Gsm-Client-Connection.mdgsm.json' + #13#10 + #13#10 +
          'هیچ رمز دیتابیس یا کلید خصوصی در این فایل وجود ندارد.',
          mbInformation, MB_OK);
      end
      else
      begin
        MsgBox(
          'نصب سرور با HTTPS محلی انجام شد، اما Windows یک شبکهٔ خصوصی مناسب و یکتا تشخیص نداد.' + #13#10 + #13#10 +
          'برای جلوگیری از دسترسی ناخواسته، اتصال رایانه‌های دیگر فعال نشد. اگر شبکهٔ فعلی، شبکهٔ داخلی و مورد اعتماد شماست، آن را در تنظیمات Windows روی Private قرار دهید و سپس نصب‌کننده را با گزینهٔ Repair دوباره اجرا کنید.' + #13#10 + #13#10 +
          'پایگاه داده PostgreSQL همچنان فقط روی همین رایانه قابل دسترسی است.',
          mbInformation, MB_OK);
      end;
    end;
  end;
end;

procedure DeinitializeSetup();
var
  ResultCode: Integer;
  PowerShellPath: String;
  Parameters: String;
begin
  // If Setup exits or raises after Prepare, restore the original service state.
  // A successful Install phase removes this state record, making this a no-op.
  if (ServiceStatePath <> '') and FileExists(ServiceStatePath) then
  begin
    PowerShellPath := ExpandConstant('{sys}\WindowsPowerShell\v1.0\powershell.exe');
    Parameters := '-NoProfile -NonInteractive -ExecutionPolicy Bypass -File "' +
      ServiceSetupScriptPath + '" -InstallDirectory "' + ExpandConstant('{app}') +
      '" -ServiceStatePath "' + ServiceStatePath + '" -Phase "Restore"';
    if Exec(PowerShellPath, Parameters, ExpandConstant('{tmp}'), SW_HIDE, ewWaitUntilTerminated, ResultCode) then
    begin
      if ResultCode <> 0 then
        Log('WARNING: API service state restore phase failed with exit code ' + IntToStr(ResultCode));
    end
    else
      Log('WARNING: Could not start API service state restore phase after Setup termination.');
  end;
end;

[UninstallRun]
Filename: "{sys}\WindowsPowerShell\v1.0\powershell.exe"; Parameters: "-NoProfile -NonInteractive -ExecutionPolicy Bypass -File ""{app}\Uninstall-MDGsmApiService.ps1"" -ServiceName ""MDGsmApi"""; RunOnceId: "MDGsmApiServiceCleanup"; Flags: runhidden waituntilterminated
