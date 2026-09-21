#define MyAppName "QuickTrans"
#define MyAppVersion "0.1.8"
#define MyAppPublisher "QuickTrans"
#define MyAppExeName "QuickTrans.exe"

[Setup]
AppId={{A1C4DBAE-AE67-4C26-8D4C-98BFAB2080D2}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher={#MyAppPublisher}
DefaultDirName={localappdata}\Programs\QuickTrans
DefaultGroupName=QuickTrans
PrivilegesRequired=lowest
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
OutputDir=..\outputs
OutputBaseFilename=QuickTrans-{#MyAppVersion}-win-x64-setup
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
SetupIconFile=..\src\QuickTrans\Assets\QuickTrans.ico
CloseApplications=yes
UninstallDisplayIcon={app}\{#MyAppExeName}

[Files]
Source: "..\src\QuickTrans\bin\Release\net8.0-windows10.0.19041.0\win-x64\publish\QuickTrans.exe"; DestDir: "{app}"; Flags: ignoreversion

[Icons]
Name: "{group}\QuickTrans"; Filename: "{app}\{#MyAppExeName}"
Name: "{userdesktop}\QuickTrans"; Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon

[Tasks]
Name: "desktopicon"; Description: "创建桌面快捷方式"; GroupDescription: "附加图标："; Flags: unchecked

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "启动 QuickTrans"; Flags: nowait postinstall skipifsilent

[Code]
procedure CurUninstallStepChanged(CurUninstallStep: TUninstallStep);
begin
  if CurUninstallStep = usUninstall then
  begin
    if (not UninstallSilent) and
      (MsgBox('是否保留 QuickTrans 的本地配置？' + #13#10 +
      '选择“否”将删除 API 凭据和设置。', mbConfirmation, MB_YESNO) = IDNO) then
      DelTree(ExpandConstant('{localappdata}\QuickTrans'), True, True, True);
  end;
end;
