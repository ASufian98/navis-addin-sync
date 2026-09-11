; BINA Navis Sync — Inno Setup installer
;
; Requires admin (writes to Program Files). Double-click = progress bar = installed.
; Silent: BinaNavisSync-<ver>-setup.exe /VERYSILENT
;
; Build:
;   ISCC installer\BinaNavis.iss /DAppVersion=0.0.1 /DPluginDir=bin\Release\net48
;
; Layout:
;   C:\Program Files\Autodesk\Navisworks Manage 20XX\Plugins\NavisWebAppSync\
;     NavisWebAppSync.dll
;     BinaRibbon.xaml
;     Resources\*.png
;   %LocalAppData%\Bina\NavisSync\versions\<ver>\  (same files, for OTA staging)

#ifndef AppVersion
  #define AppVersion "0.0.0"
#endif
#ifndef PluginDir
  #define PluginDir "..\bin\Release\net48"
#endif

; Navisworks version years to install to
#define NavisYears "2027,2026,2025"

[Setup]
AppId={{7B2F9E31-8A54-4C6D-9E18-2D5A0C8B4F67}
AppName=BINA Navis Sync
AppPublisher=Bina Cloudtech Sdn Bhd
AppPublisherURL=https://app.bina.cloud
AppVersion={#AppVersion}
DefaultDirName={localappdata}\Bina\NavisSync
PrivilegesRequired=admin
OutputDir=.
OutputBaseFilename=BinaNavisSync-{#AppVersion}-setup
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
DisableWelcomePage=yes
DisableDirPage=yes
DisableProgramGroupPage=yes
DisableReadyPage=yes
DisableFinishedPage=yes
Uninstallable=yes
UninstallDisplayName=BINA Navis Sync

[Files]
; Install to versioned folder for OTA staging/rollback
Source: "{#PluginDir}\NavisWebAppSync.dll"; DestDir: "{localappdata}\Bina\NavisSync\versions\{#AppVersion}"; Flags: ignoreversion
Source: "{#PluginDir}\BinaRibbon.xaml"; DestDir: "{localappdata}\Bina\NavisSync\versions\{#AppVersion}"; Flags: ignoreversion
Source: "{#PluginDir}\Resources\*"; DestDir: "{localappdata}\Bina\NavisSync\versions\{#AppVersion}\Resources"; Flags: ignoreversion recursesubdirs
Source: "{#PluginDir}\Newtonsoft.Json.dll"; DestDir: "{localappdata}\Bina\NavisSync\versions\{#AppVersion}"; Flags: ignoreversion

; Install to Navisworks plugin folders (detected at runtime)
; 2027
Source: "{#PluginDir}\NavisWebAppSync.dll"; DestDir: "{commonpf}\Autodesk\Navisworks Manage 2027\Plugins\NavisWebAppSync"; Flags: ignoreversion; Check: NavisInstalled('2027')
Source: "{#PluginDir}\BinaRibbon.xaml"; DestDir: "{commonpf}\Autodesk\Navisworks Manage 2027\Plugins\NavisWebAppSync"; Flags: ignoreversion; Check: NavisInstalled('2027')
Source: "{#PluginDir}\Resources\*"; DestDir: "{commonpf}\Autodesk\Navisworks Manage 2027\Plugins\NavisWebAppSync\Resources"; Flags: ignoreversion recursesubdirs; Check: NavisInstalled('2027')
Source: "{#PluginDir}\Newtonsoft.Json.dll"; DestDir: "{commonpf}\Autodesk\Navisworks Manage 2027\Plugins\NavisWebAppSync"; Flags: ignoreversion; Check: NavisInstalled('2027')
; 2026
Source: "{#PluginDir}\NavisWebAppSync.dll"; DestDir: "{commonpf}\Autodesk\Navisworks Manage 2026\Plugins\NavisWebAppSync"; Flags: ignoreversion; Check: NavisInstalled('2026')
Source: "{#PluginDir}\BinaRibbon.xaml"; DestDir: "{commonpf}\Autodesk\Navisworks Manage 2026\Plugins\NavisWebAppSync"; Flags: ignoreversion; Check: NavisInstalled('2026')
Source: "{#PluginDir}\Resources\*"; DestDir: "{commonpf}\Autodesk\Navisworks Manage 2026\Plugins\NavisWebAppSync\Resources"; Flags: ignoreversion recursesubdirs; Check: NavisInstalled('2026')
Source: "{#PluginDir}\Newtonsoft.Json.dll"; DestDir: "{commonpf}\Autodesk\Navisworks Manage 2026\Plugins\NavisWebAppSync"; Flags: ignoreversion; Check: NavisInstalled('2026')
; 2025
Source: "{#PluginDir}\NavisWebAppSync.dll"; DestDir: "{commonpf}\Autodesk\Navisworks Manage 2025\Plugins\NavisWebAppSync"; Flags: ignoreversion; Check: NavisInstalled('2025')
Source: "{#PluginDir}\BinaRibbon.xaml"; DestDir: "{commonpf}\Autodesk\Navisworks Manage 2025\Plugins\NavisWebAppSync"; Flags: ignoreversion; Check: NavisInstalled('2025')
Source: "{#PluginDir}\Resources\*"; DestDir: "{commonpf}\Autodesk\Navisworks Manage 2025\Plugins\NavisWebAppSync\Resources"; Flags: ignoreversion recursesubdirs; Check: NavisInstalled('2025')
Source: "{#PluginDir}\Newtonsoft.Json.dll"; DestDir: "{commonpf}\Autodesk\Navisworks Manage 2025\Plugins\NavisWebAppSync"; Flags: ignoreversion; Check: NavisInstalled('2025')

[Code]
function NavisInstalled(Year: String): Boolean;
var
  NavisPath: String;
begin
  NavisPath := ExpandConstant('{commonpf}\Autodesk\Navisworks Manage ' + Year);
  Result := DirExists(NavisPath);
end;

[Messages]
SetupAppTitle=BINA Navis Sync
SetupWindowTitle=BINA Navis Sync Setup
