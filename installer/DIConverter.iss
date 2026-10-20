#define AppName "DIConverter"
#define AppVersion "1.0.0"

[Setup]
AppId={{D0FD4A16-C7A3-4D56-BD03-7517BA34957F}
AppName={#AppName}
AppVersion={#AppVersion}
AppPublisher=homerick
DefaultDirName={localappdata}\Programs\DIConverter
DefaultGroupName=DIConverter
DisableProgramGroupPage=yes
PrivilegesRequired=lowest
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
MinVersion=10.0.19041
OutputDir=..\dist
OutputBaseFilename=DIConverter-Setup-{#AppVersion}-x64
SetupIconFile=..\Assets\DIConverter.ico
UninstallDisplayIcon={app}\DIConverter.exe
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
WizardSizePercent=110
CloseApplications=yes
RestartApplications=no
VersionInfoVersion=1.0.0.0
VersionInfoDescription=DIConverter Setup
VersionInfoProductName=DIConverter

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"
Name: "russian"; MessagesFile: "compiler:Languages\Russian.isl"
Name: "ukrainian"; MessagesFile: "compiler:Languages\Ukrainian.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked

[Files]
Source: "..\DIConverterApp\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs; Excludes: "*.pdb,*-preview.png,*-check.txt"

[Icons]
Name: "{group}\DIConverter"; Filename: "{app}\DIConverter.exe"
Name: "{autodesktop}\DIConverter"; Filename: "{app}\DIConverter.exe"; Tasks: desktopicon

[Run]
Filename: "{app}\DIConverter.exe"; Description: "{cm:LaunchProgram,DIConverter}"; Flags: nowait postinstall skipifsilent
