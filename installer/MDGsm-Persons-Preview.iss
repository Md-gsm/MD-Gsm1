#pragma codepage 65001
#ifndef PublishDir
  #define PublishDir AddBackslash(SourcePath) + "..\artifacts\windows-publish"
#endif
#ifndef OutputDir
  #define OutputDir AddBackslash(SourcePath) + "..\artifacts\installer"
#endif

#define AppName "نرم‌افزار جامع مدیریت و حسابداری تعمیرگاه موبایل"
#define AppVersion "0.2.11"
#define AppPublisher "MD-Gsm"
#define AppExeName "MDGsm.Desktop.exe"

[Setup]
AppId={{2F4D1327-DF50-4E3E-9397-A4B7F880D020}
AppName={#AppName}
AppVersion={#AppVersion}
AppPublisher={#AppPublisher}
AppComments=Interactive UI preview for the MD-Gsm Persons module. Preview-only; no business database is created or modified.
DefaultDirName={localappdata}\Programs\MD-Gsm-Persons-Preview
DefaultGroupName={#AppName}
DisableProgramGroupPage=yes
PrivilegesRequired=lowest
ArchitecturesAllowed=x64
ArchitecturesInstallIn64BitMode=x64
OutputDir={#OutputDir}
OutputBaseFilename=MD-Gsm-Persons-Preview-Setup-v0.2.11
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
UninstallDisplayName={#AppName}
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

[Run]
Filename: "{app}\{#AppExeName}"; Description: "اجرای نرم‌افزار جامع مدیریت و حسابداری تعمیرگاه موبایل"; Flags: postinstall nowait skipifsilent
