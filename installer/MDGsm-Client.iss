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

[Icons]
Name: "{group}\نرم‌افزار جامع مدیریت و حسابداری تعمیرگاه موبایل"; Filename: "{app}\{#AppExeName}"
Name: "{autodesktop}\نرم‌افزار جامع مدیریت و حسابداری تعمیرگاه موبایل"; Filename: "{app}\{#AppExeName}"; Tasks: desktopicon

[Code]
var
  ApiPage: TInputQueryWizardPage;
  ApiBaseUrl: String;

function IsSafeHttpsBaseUrl(const Value: String): Boolean;
var
  I: Integer;
  C: Char;
  LowerValue: String;
begin
  LowerValue := LowerCase(Trim(Value));
  if Copy(LowerValue, 1, 8) <> 'https://' then
    Result := False
  else if (Pos('@', Copy(Value, 9, Length(Value))) > 0) or
          (Pos('?', Value) > 0) or (Pos('#', Value) > 0) or
          (Pos(' ', Value) > 0) or (Pos('\', Value) > 0) then
    Result := False
  else
  begin
    Result := True;
    for I := 1 to Length(Value) do
    begin
      C := Value[I];
      if not ((C >= 'a') and (C <= 'z')) and
         not ((C >= 'A') and (C <= 'Z')) and
         not ((C >= '0') and (C <= '9')) and
         (C <> ':') and (C <> '/') and (C <> '.') and
         (C <> '-') and (C <> '_') and (C <> '[') and (C <> ']') and (C <> '%') then
      begin
        Result := False;
        Exit;
      end;
    end;
  end;
end;

procedure InitializeWizard;
begin
  ApiPage := CreateInputQueryPage(wpSelectDir,
    'Central API address', 'Connect this client to the central MD-Gsm API',
    'Enter the HTTPS base address of the central server. PostgreSQL credentials are never requested on a client.');
  ApiPage.Add('Central API HTTPS URL', False);
  ApiPage.Values[0] := 'https://central-server:5081/';
end;

function NextButtonClick(CurPageID: Integer): Boolean;
var
  Candidate: String;
begin
  Result := True;
  if CurPageID = ApiPage.ID then
  begin
    Candidate := Trim(ApiPage.Values[0]);
    if WizardSilent then
      Candidate := GetEnv('MDGSM_CLIENT_API_BASEURL');
    if not IsSafeHttpsBaseUrl(Candidate) then
    begin
      MsgBox('A valid HTTPS central API address is required. HTTP is not allowed for remote clients.', mbError, MB_OK);
      Result := False;
      Exit;
    end;
    if Candidate[Length(Candidate)] <> '/' then
      Candidate := Candidate + '/';
    ApiBaseUrl := Candidate;
  end;
end;

procedure CurStepChanged(CurStep: TSetupStep);
var
  ConfigPath: String;
  ConfigText: String;
begin
  if CurStep = ssPostInstall then
  begin
    ConfigPath := ExpandConstant('{app}\appsettings.json');
    ConfigText := '{"Api":{"BaseUrl":"' + ApiBaseUrl + '","TimeoutSeconds":5}}' + #13#10;
    if not SaveStringToFile(ConfigPath, ConfigText, False) then
      RaiseException('Could not save the central API address for this client.');
  end;
end;
