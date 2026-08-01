#ifndef AppVersion
  #error AppVersion must be supplied by build-installer.ps1.
#endif

#ifndef SourceDir
  #error SourceDir must be supplied by build-installer.ps1.
#endif

#ifndef OutputDir
  #error OutputDir must be supplied by build-installer.ps1.
#endif

#define AppName "PSVR2 iRacing Haptics"
#define AppExeName "PSVR2iRacingHaptics.exe"
#define ProjectUrl "https://github.com/nishizumi-maho/PSVR2-iRacing-Haptics"

[Setup]
AppId={{B567B185-C843-49AB-9B1A-97E01BFD1212}
AppName={#AppName}
AppVersion={#AppVersion}
AppVerName={#AppName} {#AppVersion}
AppPublisher=Nishizumi Maho
AppPublisherURL={#ProjectUrl}
AppSupportURL={#ProjectUrl}/issues
AppUpdatesURL={#ProjectUrl}/releases/latest
VersionInfoVersion={#AppVersion}.0
DefaultDirName={localappdata}\Programs\PSVR2 iRacing Haptics
DefaultGroupName={#AppName}
DisableProgramGroupPage=yes
PrivilegesRequired=lowest
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
MinVersion=10.0
OutputDir={#OutputDir}
OutputBaseFilename=PSVR2-iRacing-Haptics-v{#AppVersion}-win-x64-setup
SetupIconFile=..\assets\app.ico
UninstallDisplayIcon={app}\{#AppExeName}
Compression=lzma2/max
SolidCompression=yes
WizardStyle=modern
CloseApplications=yes
RestartApplications=no
SetupLogging=yes
UsePreviousAppDir=yes
UsePreviousGroup=yes
UsePreviousTasks=yes
LicenseFile=..\LICENSE

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "Create a desktop shortcut"; GroupDescription: "Additional shortcuts:"; Flags: unchecked

[Files]
Source: "{#SourceDir}\*"; DestDir: "{app}"; Excludes: "\portable.mode,\data\*"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{group}\{#AppName}"; Filename: "{app}\{#AppExeName}"; WorkingDir: "{app}"
Name: "{group}\Uninstall {#AppName}"; Filename: "{uninstallexe}"
Name: "{autodesktop}\{#AppName}"; Filename: "{app}\{#AppExeName}"; WorkingDir: "{app}"; Tasks: desktopicon

[Run]
Filename: "{app}\{#AppExeName}"; Description: "Launch {#AppName}"; WorkingDir: "{app}"; Flags: nowait postinstall skipifsilent
