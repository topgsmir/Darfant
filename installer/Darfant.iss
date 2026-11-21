#ifndef PayloadDir
  #error PayloadDir must point to a clean Build-Darfant.ps1 output.
#endif
#ifndef AppVersion
  #error AppVersion must match Darfant/Branding.props.
#endif
#ifndef InstallerOutputDir
  #error InstallerOutputDir is required.
#endif

[Setup]
AppId={{7E56FFAE-074B-4ED6-AFF9-7E1E35DBE5A7}
AppName=دارفانت
AppVersion={#AppVersion}
AppVerName=دارفانت {#AppVersion}
AppPublisher=Darfant
AppPublisherURL=https://github.com/topgsmir/Darfant
AppSupportURL=https://github.com/topgsmir/Darfant/issues
AppUpdatesURL=https://github.com/topgsmir/Darfant/releases/latest
DefaultDirName={localappdata}\Programs\Darfant
PrivilegesRequired=lowest
ArchitecturesAllowed=x64os
ArchitecturesInstallIn64BitMode=x64os
MinVersion=10.0.19041
DisableWelcomePage=no
DisableDirPage=yes
DisableProgramGroupPage=yes
DisableReadyPage=yes
UsePreviousAppDir=yes
WizardStyle=modern
SetupIconFile=..\v2rayN\v2rayN\Resources\Darfant.ico
UninstallDisplayIcon={app}\Darfant.exe
UninstallDisplayName=دارفانت
VersionInfoVersion={#AppVersion}
OutputDir={#InstallerOutputDir}
OutputBaseFilename=Darfant-Setup-x64
Compression=lzma2/normal
SolidCompression=yes
DiskSpanning=no
CloseApplications=yes
RestartApplications=no
SetupLogging=yes
LanguageDetectionMethod=none
ShowLanguageDialog=no

[Languages]
Name: "farsi"; MessagesFile: "Languages\Farsi.isl"

[Messages]
WelcomeLabel1=نصب دارفانت
WelcomeLabel2=دارفانت روی رایانهٔ شما نصب می‌شود و میان‌بر آن روی دسکتاپ و منوی استارت قرار می‌گیرد.%n%nبرای به‌روزرسانی، ابتدا اتصال را قطع کرده و از دارفانت خارج شوید.
FinishedHeadingLabel=دارفانت آماده است
FinishedLabel=نصب با موفقیت انجام شد. از این پس برای اجرای برنامه، روی آیکون دارفانت در دسکتاپ یا منوی استارت کلیک کنید.%n%nهنگام اجرای برنامه، درخواست دسترسی مدیر ویندوز را تأیید کنید.

[Files]
Source: "{#PayloadDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{userdesktop}\دارفانت"; Filename: "{app}\Darfant.exe"; WorkingDir: "{app}"; AppUserModelID: "Darfant.Windows"
Name: "{userprograms}\دارفانت"; Filename: "{app}\Darfant.exe"; WorkingDir: "{app}"; AppUserModelID: "Darfant.Windows"

[Run]
Filename: "{app}\Darfant.exe"; Description: "اجرای دارفانت"; Verb: "runas"; WorkingDir: "{app}"; Flags: postinstall shellexec nowait skipifsilent

; No recursive UninstallDelete: generated subscriptions and settings survive uninstall and upgrades.
