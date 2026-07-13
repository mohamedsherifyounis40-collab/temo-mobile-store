; ==========================================================================
; سكريبت Inno Setup لبناء أداة تثبيت Temo Mobile Store
; ==========================================================================
; ملحوظة مهمة: البرنامج هيتنصب في مجلد بسيط (مش Program Files) عشان
; قاعدة البيانات وملف الترخيص يقدروا يتكتبوا فيه من غير أي مشاكل صلاحيات.
;
; ملحوظة أهم: قاعدة البيانات (TemoStoreDB.db*) وملف الترخيص (license.dat) وملف
; لوج السيرفر البعيد مستبعدين عمدًا من [Files] تحت (Excludes). لو دول اتحطوا
; بالغلط، وقت ما عميل حالي (عنده بيانات حقيقية) يشغّل نسخة تحديث من المُنصِّب
; ده، بياناته الحقيقية هتتمسح وتتستبدل بأي ملف تجريبي كان موجود على جهاز
; المطوّر وقت البناء. الاستبعاد ده بيمنع الكارثة دي تحصل تاني.
; ==========================================================================

#define MyAppName "Temo Mobile Store"
#define MyAppVersion "1.1"
#define MyAppPublisher "Temo Mobile Store"
#define MyAppExeName "Temo Mobile Store.exe"

[Setup]
AppId={{A1B2C3D4-1234-5678-9ABC-TEMOMOBILESTORE}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher={#MyAppPublisher}
DefaultDirName={sd}\TemoMobileStore
DefaultGroupName={#MyAppName}
DisableProgramGroupPage=yes
OutputDir=.\Output
OutputBaseFilename=TemoMobileStore_Setup
Compression=lzma
SolidCompression=yes
WizardStyle=modern
PrivilegesRequired=lowest
ArchitecturesInstallIn64BitMode=x64compatible

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "إنشاء أيقونة على سطح المكتب"; GroupDescription: "أيقونات إضافية:"

; ==========================================================================
; المسار هنا نسبي لمكان السكريبت نفسه ({#SourcePath}) عشان يشتغل صح أيًا كان
; مكان المشروع على الجهاز، بدل ما يتقفل على "D:\..." بس.
; ==========================================================================
[Files]
Source: "{#SourcePath}\..\Temo Mobile Store\bin\Release\net10.0-windows\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs; Excludes: "TemoStoreDB.db,TemoStoreDB.db-shm,TemoStoreDB.db-wal,license.dat,remote_server_log.txt"

[Icons]
Name: "{group}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"
Name: "{group}\إلغاء تثبيت {#MyAppName}"; Filename: "{uninstallexe}"
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "تشغيل {#MyAppName} الآن"; Flags: nowait postinstall skipifsilent

[UninstallDelete]
; ملحوظة: مش بنمسح قاعدة البيانات أو ملف الترخيص عند الحذف، حفاظًا على بيانات المحل
