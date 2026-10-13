#ifndef AppVersion
  #define AppVersion "0.5.2"
#endif
#ifndef PayloadDir
  #define PayloadDir "..\..\work\publish-v05"
#endif
#ifndef DocsDir
  #define DocsDir ".."
#endif
#ifndef OutDir
  #define OutDir ".."
#endif
#ifdef TestBuild
  #define AppKey "DateiWerk-Installertest"
  #define AppName "DateiWerk Installertest"
#else
  #define AppKey "DateiWerk"
  #define AppName "DateiWerk"
#endif
#define TermsVersion "2026-10-13.1"

[Setup]
AppId={#AppKey}-nnifFlame
AppName={#AppName}
AppVersion={#AppVersion}
AppPublisher=DateiWerk Open-Source-Projekt
AppPublisherURL=https://github.com/nnifFlame/DateiWerk
AppSupportURL=https://github.com/nnifFlame/DateiWerk/issues
AppUpdatesURL=https://github.com/nnifFlame/DateiWerk/releases
DefaultDirName={localappdata}\Programs\{#AppKey}
DefaultGroupName={#AppName}
DisableDirPage=yes
DisableProgramGroupPage=yes
PrivilegesRequired=lowest
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
LicenseFile={#DocsDir}\NUTZUNGSBEDINGUNGEN.txt
InfoAfterFile={#DocsDir}\INSTALLATION.txt
OutputDir={#OutDir}
OutputBaseFilename=DateiWerk-Setup
SetupArchitecture=x64
SetupIconFile=DateiWerk.ico
Compression=lzma2/fast
SolidCompression=yes
WizardStyle=modern
UninstallDisplayIcon={app}\DateiWerk.exe
ChangesAssociations=yes
CloseApplications=yes
CloseApplicationsFilter=DateiWerk.exe
RestartApplications=no
SetupLogging=yes
VersionInfoVersion={#AppVersion}

[Languages]
Name: german; MessagesFile: compiler:Languages\German.isl

[Tasks]
Name: desktopicon; Description: "Desktop-Verknüpfung erstellen"; GroupDescription: "Verknüpfungen:"
Name: explorermenu; Description: "DateiWerk im Explorer-Rechtsklick-Menü anzeigen"; GroupDescription: "Integration:"
Name: updates; Description: "Beim Programmstart nach Updates suchen und automatisch aktualisieren"; GroupDescription: "Updates:"

[Files]
Source: "{#PayloadDir}\DateiWerk.exe"; DestDir: "{app}"; Flags: ignoreversion
Source: "{#DocsDir}\NUTZUNGSBEDINGUNGEN.txt"; DestDir: "{app}"; Flags: ignoreversion
Source: "{#DocsDir}\LICENSE"; DestDir: "{app}"; Flags: ignoreversion
Source: "{#DocsDir}\INSTALLATION.txt"; DestDir: "{app}"; Flags: ignoreversion
Source: "{#DocsDir}\LIESMICH.md"; DestDir: "{app}"; Flags: ignoreversion
Source: "{#DocsDir}\Lizenzen\*"; DestDir: "{app}\Lizenzen"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{userprograms}\{#AppName}\DateiWerk"; Filename: "{app}\DateiWerk.exe"
Name: "{userprograms}\{#AppName}\Deinstallieren"; Filename: "{uninstallexe}"
Name: "{userdesktop}\{#AppName}"; Filename: "{app}\DateiWerk.exe"; Tasks: desktopicon

[Registry]
Root: HKCU; Subkey: "Software\{#AppKey}"; ValueType: string; ValueName: InstallPath; ValueData: "{app}"; Flags: uninsdeletekey
Root: HKCU; Subkey: "Software\Classes\*\shell\{#AppKey}"; ValueType: string; ValueName: ""; ValueData: "{#AppName}"; Tasks: explorermenu; Flags: uninsdeletekey
Root: HKCU; Subkey: "Software\Classes\*\shell\{#AppKey}"; ValueType: string; ValueName: Icon; ValueData: """{app}\DateiWerk.exe"",0"; Tasks: explorermenu
Root: HKCU; Subkey: "Software\Classes\*\shell\{#AppKey}"; ValueType: string; ValueName: MultiSelectModel; ValueData: Single; Tasks: explorermenu
Root: HKCU; Subkey: "Software\Classes\*\shell\{#AppKey}\command"; ValueType: string; ValueName: ""; ValueData: """{app}\DateiWerk.exe"" --open ""%1"""; Tasks: explorermenu
Root: HKCU; Subkey: "Software\Classes\Directory\shell\{#AppKey}"; ValueType: string; ValueName: ""; ValueData: "{#AppName}"; Tasks: explorermenu; Flags: uninsdeletekey
Root: HKCU; Subkey: "Software\Classes\Directory\shell\{#AppKey}"; ValueType: string; ValueName: Icon; ValueData: """{app}\DateiWerk.exe"",0"; Tasks: explorermenu
Root: HKCU; Subkey: "Software\Classes\Directory\shell\{#AppKey}"; ValueType: string; ValueName: MultiSelectModel; ValueData: Single; Tasks: explorermenu
Root: HKCU; Subkey: "Software\Classes\Directory\shell\{#AppKey}\command"; ValueType: string; ValueName: ""; ValueData: """{app}\DateiWerk.exe"" --open ""%1"""; Tasks: explorermenu

[Run]
Filename: "{app}\DateiWerk.exe"; Description: "DateiWerk starten"; Flags: nowait postinstall skipifsilent; Check: not IsUpdate
Filename: "{app}\DateiWerk.exe"; Flags: nowait skipifnotsilent; Check: IsUpdate

[Code]
function IsUpdate: Boolean;
begin
  Result := ExpandConstant('{param:UPDATE|0}') <> '0';
end;

function InitializeSetup: Boolean;
var Accepted: String;
begin
  Result := True;
#ifndef TestBuild
  if WizardSilent then begin
    if not RegQueryStringValue(HKCU, 'Software\{#AppKey}', 'TermsVersion', Accepted) then Accepted := '';
    if Accepted <> '{#TermsVersion}' then begin
      Result := False;
      Log('Stille Installation ohne vorherige Zustimmung zu dieser Bedingungsversion abgewiesen.');
    end;
  end;
#endif
end;

procedure CurStepChanged(CurStep: TSetupStep);
var Automatic: Cardinal;
begin
  if CurStep = ssPostInstall then begin
    RegWriteStringValue(HKCU, 'Software\{#AppKey}', 'TermsVersion', '{#TermsVersion}');
    if WizardIsTaskSelected('updates') then Automatic := 1 else Automatic := 0;
    RegWriteDWordValue(HKCU, 'Software\{#AppKey}', 'AutomaticUpdates', Automatic);
    RegWriteStringValue(HKCU, 'Software\{#AppKey}', 'Version', '{#AppVersion}');
  end;
end;

