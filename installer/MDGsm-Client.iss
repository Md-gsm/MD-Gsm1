#ifndef PublishDir
  #define PublishDir AddBackslash(SourcePath) + "..\artifacts\windows-publish\desktop"
#endif
#ifndef OutputDir
  #define OutputDir AddBackslash(SourcePath) + "..\artifacts\installer"
#endif

#define AppName "MD-Gsm — Client"
#define AppVersion "0.3.0"
#define AppPublisher "MD-Gsm"
#define AppExeName "MDGsm.Desktop.exe"

[Setup]
AppId={{9B6E1B7C-3F12-4A11-8C71-7E4A1B9F2201}
AppName={#AppName}
AppVersion={#AppVersion}
AppPublisher={#AppPublisher}
AppComments=Client-only installation. Does not install PostgreSQL or the MDGsmApi Windows service.
DefaultDirName={autopf}\MD-Gsm
DisableDirPage=yes
DefaultGroupName={#AppName}
DisableProgramGroupPage=yes
PrivilegesRequired=admin
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
OutputDir={#OutputDir}
OutputBaseFilename=MD-Gsm-Client-Setup-v0.3.0
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
Uninstallable=yes
CloseApplications=yes
RestartApplications=no
SetupLogging=yes

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "Create a desktop shortcut"; GroupDescription: "Additional shortcuts:"; Flags: unchecked

[Files]
Source: "{#PublishDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs
Source: "{#SourcePath}\Import-MDGsmClientConnection.ps1"; DestDir: "{app}"; Flags: ignoreversion
Source: "{#SourcePath}\\Uninstall-MDGsmClientTrust.ps1"; DestDir: "{app}"; Flags: ignoreversion

[Icons]
Name: "{group}\نرم‌افزار جامع مدیریت و حسابداری تعمیرگاه موبایل"; Filename: "{app}\{#AppExeName}"
Name: "{autodesktop}\نرم‌افزار جامع مدیریت و حسابداری تعمیرگاه موبایل"; Filename: "{app}\{#AppExeName}"; Tasks: desktopicon

[Code]
var
  ConnectionPage: TInputFileWizardPage;
  ConnectionFilePath: String;

function InitializeSetup: Boolean;
var
  Candidate: String;
begin
  Result := True;
  if WizardSilent then
  begin
    Candidate := Trim(GetEnv('MDGSM_CLIENT_CONNECTION_FILE'));
    if (Candidate = '') or (not FileExists(Candidate)) then
    begin
      MsgBox('Silent client setup requires MDGSM_CLIENT_CONNECTION_FILE pointing to a trusted central connection bundle.', mbError, MB_OK);
      Result := False;
      Exit;
    end;
    ConnectionFilePath := Candidate;
  end;
end;

procedure InitializeWizard;
begin
  ConnectionPage := CreateInputFilePage(wpSelectDir,
    'Central server connection', 'Connect this client to the central MD-Gsm server',
    'Select the connection file exported by the central server. Copy it using trusted physical media or an already authenticated channel. The file contains no database password.');
  ConnectionPage.Add('MD-Gsm connection file', '*.mdgsm.json');
end;

function NextButtonClick(CurPageID: Integer): Boolean;
var
  Candidate: String;
begin
  Result := True;
  if CurPageID = ConnectionPage.ID then
  begin
    Candidate := Trim(ConnectionPage.Values[0]);
    if WizardSilent then
      Candidate := GetEnv('MDGSM_CLIENT_CONNECTION_FILE');
    if (Candidate = '') or (not FileExists(Candidate)) then
    begin
      MsgBox('Select a valid MD-Gsm connection file exported by the central server.', mbError, MB_OK);
      Result := False;
      Exit;
    end;
    ConnectionFilePath := Candidate;
  end;
end;

procedure CurStepChanged(CurStep: TSetupStep);
var
  ResultCode: Integer;
  PowerShellPath: String;
  Parameters: String;
begin
  if CurStep = ssPostInstall then
  begin
    PowerShellPath := ExpandConstant('{sys}\WindowsPowerShell\v1.0\powershell.exe');
    Parameters := '-NoProfile -NonInteractive -ExecutionPolicy Bypass -File "' +
      ExpandConstant('{app}\Import-MDGsmClientConnection.ps1') + '" -ConnectionFile "' +
      ConnectionFilePath + '" -InstallDirectory "' + ExpandConstant('{app}') + '"';
    if not Exec(PowerShellPath, Parameters, ExpandConstant('{app}'), SW_HIDE, ewWaitUntilTerminated, ResultCode) then
      RaiseException('Could not start the central HTTPS connection import.');
    if ResultCode <> 0 then
      RaiseException('The connection file failed certificate or HTTPS verification. The client connection was not configured.');
  end;
end;


[UninstallRun]
Filename: "{sys}\WindowsPowerShell\v1.0\powershell.exe"; Parameters: "-NoProfile -NonInteractive -ExecutionPolicy Bypass -File ""{app}\Uninstall-MDGsmClientTrust.ps1"""; RunOnceId: "MDGsmClientTlsTrustCleanup"; Flags: runhidden waituntilterminated
