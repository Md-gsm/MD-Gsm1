#pragma codepage 65001
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
DefaultGroupName={#AppName}
DisableProgramGroupPage=yes
PrivilegesRequired=admin
ArchitecturesAllowed=x64
ArchitecturesInstallIn64BitMode=x64
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
Source: "{#SourcePath}\Uninstall-MDGsmApiService.ps1"; DestDir: "{app}"; Flags: ignoreversion

[Icons]
Name: "{group}\نرم‌افزار جامع مدیریت و حسابداری تعمیرگاه موبایل"; Filename: "{app}\{#AppExeName}"
Name: "{autodesktop}\نرم‌افزار جامع مدیریت و حسابداری تعمیرگاه موبایل"; Filename: "{app}\{#AppExeName}"; Tasks: desktopicon

[Code]
procedure RunApiServiceSetup();
var
  ResultCode: Integer;
  PowerShellPath: String;
  Parameters: String;
begin
  PowerShellPath := ExpandConstant('{sys}\WindowsPowerShell\v1.0\powershell.exe');
  Parameters := '-NoProfile -NonInteractive -ExecutionPolicy Bypass -File "' +
    ExpandConstant('{app}\Install-MDGsmApiService.ps1') + '" -InstallDirectory "' +
    ExpandConstant('{app}') + '"';
  if not Exec(PowerShellPath, Parameters, ExpandConstant('{app}'), SW_HIDE, ewWaitUntilTerminated, ResultCode) then
    RaiseException('Could not start the MD-Gsm API service setup script.');
  if ResultCode <> 0 then
    RaiseException('MD-Gsm API service setup failed. The installation was not completed.');
end;

procedure CurStepChanged(CurStep: TSetupStep);
begin
  if CurStep = ssPostInstall then
    RunApiServiceSetup();
end;

[UninstallRun]
Filename: "{sys}\WindowsPowerShell\v1.0\powershell.exe"; Parameters: "-NoProfile -NonInteractive -ExecutionPolicy Bypass -File ""{app}\Uninstall-MDGsmApiService.ps1"" -ServiceName ""MDGsmApi"""; Flags: runhidden waituntilterminated
