#define MyAppName "TrayWebApp"
#define MyAppExeName "TrayWebApp.exe"
#ifndef AppVersion
#define AppVersion "1.0.1"
#endif
#ifndef PublishDir
#define PublishDir "..\publish\win-x64-final"
#endif

[Setup]
AppId={{3E48C0BF-31E6-4F39-9899-93C9F531F68C}
AppName={#MyAppName}
AppVersion={#AppVersion}
AppPublisher=TrayWebApp
DefaultDirName={localappdata}\Programs\TrayWebApp
DefaultGroupName=TrayWebApp
DisableProgramGroupPage=yes
OutputDir=..\publish\installer
OutputBaseFilename=TrayWebApp-Setup-{#AppVersion}
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
ArchitecturesAllowed=x64compatible
PrivilegesRequired=lowest
UninstallDisplayIcon={app}\{#MyAppExeName}

[Languages]
Name: "korean"; MessagesFile: "compiler:Languages\Korean.isl"
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked
Name: "startup"; Description: "Windows 시작 시 TrayWebApp 실행"; GroupDescription: "실행 옵션:"; Flags: unchecked

[Files]
Source: "{#PublishDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{group}\TrayWebApp"; Filename: "{app}\{#MyAppExeName}"
Name: "{userdesktop}\TrayWebApp"; Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon

[Registry]
Root: HKCU; Subkey: "Software\Microsoft\Windows\CurrentVersion\Run"; ValueType: string; ValueName: "TrayWebApp"; ValueData: """{app}\{#MyAppExeName}"""; Tasks: startup; Flags: uninsdeletevalue

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "TrayWebApp 실행"; Flags: nowait postinstall skipifsilent
