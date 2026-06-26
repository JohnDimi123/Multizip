; ---------------------------------------------------------------------------
; Multizip - Inno Setup installer script
;
; Build the solution in Release first (see docs/BUILD.md), then compile this
; script with Inno Setup 6 (https://jrsoftware.org/isinfo.php) to produce
; "Multizip-Setup.exe". The installer registers Explorer context-menu entries
; and bundles 7z.dll / 7z.sfx when they are present in build\redist.
; ---------------------------------------------------------------------------

#define AppName        "Multizip"
#define AppVersion     "1.0.1"
#define AppPublisher   "Multizip"
#define AppExeName     "Multizip.exe"
#define BuildDir       "..\src\Multizip.App\bin\Release\net48"
#define PluginDir      "..\src\Multizip.Plugins.Sample\bin\Release\net48"
#define RedistDir      "redist"

[Setup]
AppId={{8F3A6C21-7B4E-4D2A-9E5C-1A2B3C4D5E6F}
AppName={#AppName}
AppVersion={#AppVersion}
AppPublisher={#AppPublisher}
DefaultDirName={autopf}\{#AppName}
DefaultGroupName={#AppName}
UninstallDisplayIcon={app}\{#AppExeName}
OutputBaseFilename=Multizip-Setup
OutputDir=output
Compression=lzma2/max
SolidCompression=yes
ArchitecturesInstallIn64BitMode=x64
MinVersion=6.1
WizardStyle=classic
DisableProgramGroupPage=yes
PrivilegesRequired=admin

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "Create a &desktop shortcut"; GroupDescription: "Shortcuts:"
Name: "contextmenu";  Description: "Add Multizip to the Windows Explorer right-click menu"; GroupDescription: "Integration:"

[Files]
; Main application (everything the build produced: exe, config, dependencies).
Source: "{#BuildDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs
; Reference plugin -> Plugins folder.
Source: "{#PluginDir}\Multizip.Plugins.Sample.dll"; DestDir: "{app}\Plugins"; Flags: ignoreversion skipifsourcedoesntexist
; Native 7-Zip engine + SFX module (optional; enables the broadest format support).
Source: "{#RedistDir}\7z.dll"; DestDir: "{app}"; Flags: ignoreversion skipifsourcedoesntexist
Source: "{#RedistDir}\7z.sfx"; DestDir: "{app}"; Flags: ignoreversion skipifsourcedoesntexist
; Documentation.
Source: "..\README.md"; DestDir: "{app}"; Flags: ignoreversion isreadme
Source: "..\LICENSE";   DestDir: "{app}"; Flags: ignoreversion

[Icons]
Name: "{group}\{#AppName}";  Filename: "{app}\{#AppExeName}"
Name: "{group}\Uninstall {#AppName}"; Filename: "{uninstallexe}"
Name: "{autodesktop}\{#AppName}"; Filename: "{app}\{#AppExeName}"; Tasks: desktopicon

[Run]
Filename: "{app}\{#AppExeName}"; Description: "Launch {#AppName}"; Flags: nowait postinstall skipifsilent

; --- Machine-wide "Add to archive" verbs (files + folders) ------------------
[Registry]
Root: HKCR; Subkey: "*\shell\Multizip.Add"; ValueType: string; ValueName: ""; ValueData: "Add to Multizip archive..."; Tasks: contextmenu; Flags: uninsdeletekey
Root: HKCR; Subkey: "*\shell\Multizip.Add"; ValueType: string; ValueName: "Icon"; ValueData: "{app}\{#AppExeName},0"; Tasks: contextmenu
Root: HKCR; Subkey: "*\shell\Multizip.Add\command"; ValueType: string; ValueName: ""; ValueData: """{app}\{#AppExeName}"" /add ""%1"""; Tasks: contextmenu
Root: HKCR; Subkey: "Directory\shell\Multizip.Add"; ValueType: string; ValueName: ""; ValueData: "Add to Multizip archive..."; Tasks: contextmenu; Flags: uninsdeletekey
Root: HKCR; Subkey: "Directory\shell\Multizip.Add"; ValueType: string; ValueName: "Icon"; ValueData: "{app}\{#AppExeName},0"; Tasks: contextmenu
Root: HKCR; Subkey: "Directory\shell\Multizip.Add\command"; ValueType: string; ValueName: ""; ValueData: """{app}\{#AppExeName}"" /add ""%1"""; Tasks: contextmenu

; Per-extension Open / Extract verbs are written by [Code] below so the long list
; of archive extensions stays maintainable.

[Code]
// Comma-separated so we avoid a typed const array (which Pascal Script rejects).
const
  ArchiveExtsCsv = '.zip,.7z,.rar,.tar,.gz,.tgz,.bz2,.xz,.zst,.cab,.arj,.lzh,.lha,.cpio,.iso,.wim,.jar,.apk';

procedure WriteVerb(const Ext, Verb, Caption, Args: string);
var
  Base, Cmd, Exe: string;
begin
  Exe := ExpandConstant('{app}\{#AppExeName}');
  Base := 'SystemFileAssociations\' + Ext + '\shell\' + Verb;
  Cmd := Base + '\command';
  RegWriteStringValue(HKEY_CLASSES_ROOT, Base, '', Caption);
  RegWriteStringValue(HKEY_CLASSES_ROOT, Base, 'Icon', Exe + ',0');
  RegWriteStringValue(HKEY_CLASSES_ROOT, Cmd, '', '"' + Exe + '" ' + Args + ' "%1"');
end;

procedure RegisterExt(const Ext: string);
begin
  WriteVerb(Ext, 'Multizip.Open',        'Open with Multizip',              '/open');
  WriteVerb(Ext, 'Multizip.ExtractHere', 'Extract here (Multizip)',         '/extracthere');
  WriteVerb(Ext, 'Multizip.ExtractTo',   'Extract to folder... (Multizip)', '/extractto');
end;

procedure RemoveExt(const Ext: string);
var
  Base: string;
begin
  Base := 'SystemFileAssociations\' + Ext + '\shell\';
  RegDeleteKeyIncludingSubkeys(HKEY_CLASSES_ROOT, Base + 'Multizip.Open');
  RegDeleteKeyIncludingSubkeys(HKEY_CLASSES_ROOT, Base + 'Multizip.ExtractHere');
  RegDeleteKeyIncludingSubkeys(HKEY_CLASSES_ROOT, Base + 'Multizip.ExtractTo');
end;

procedure ForEachExt(DoRegister: Boolean);
var
  S, Ext: string;
  P: Integer;
begin
  S := ArchiveExtsCsv + ',';
  repeat
    P := Pos(',', S);
    Ext := Copy(S, 1, P - 1);
    Delete(S, 1, P);
    if Ext <> '' then
    begin
      if DoRegister then RegisterExt(Ext) else RemoveExt(Ext);
    end;
  until S = '';
end;

procedure RegisterContextMenu;
begin
  ForEachExt(True);
end;

procedure RemoveContextMenu;
begin
  ForEachExt(False);
end;

procedure CurStepChanged(CurStep: TSetupStep);
begin
  if (CurStep = ssPostInstall) and WizardIsTaskSelected('contextmenu') then
    RegisterContextMenu;
end;

procedure CurUninstallStepChanged(CurUninstallStep: TUninstallStep);
begin
  if CurUninstallStep = usUninstall then
    RemoveContextMenu;
end;
