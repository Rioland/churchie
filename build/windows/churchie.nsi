; Churchie installer for Windows (NSIS 3).
;
; Don't run this by hand: build/make-windows-installer.sh fills in the values below.
;   VERSION  - e.g. 0.1.0
;   SRC      - folder containing Churchie.exe (the published Windows build)
;   OUTFILE  - where to write the installer, e.g. dist/Churchie-Setup-win-x64.exe
;   ICON     - path to churchie.ico
;   ART      - folder with welcome.bmp (164x314 side panel) and header.bmp (150x57)
;   UNLIST   - generated file with one "Delete" line per installed file (for the uninstaller)
;
; It installs for the current user only, so no administrator password is needed.
; The default folder is %LOCALAPPDATA%\Programs\Churchie, where per-user apps such as
; VS Code also go. Songs and settings live in %AppData%\Churchie and are never touched.

Unicode true
!include "MUI2.nsh"
!include "FileFunc.nsh"
!include "x64.nsh"

!define APPNAME   "Churchie"
!define PUBLISHER "Engr. Adedamola Emmanuel"
!define WEBSITE   "https://churchie.netlify.app"
!define UNINSTKEY "Software\Microsoft\Windows\CurrentVersion\Uninstall\Churchie"
!define UNINSTEXE "Uninstall Churchie.exe"

Name "${APPNAME}"
OutFile "${OUTFILE}"
InstallDir "$LOCALAPPDATA\Programs\Churchie"
InstallDirRegKey HKCU "Software\Churchie" "InstallDir"
RequestExecutionLevel user
SetCompressor /SOLID lzma
BrandingText "${APPNAME} ${VERSION}"

; The details Windows shows under the installer's Properties > Details.
VIProductVersion "${VERSION}.0"
VIAddVersionKey "ProductName"     "${APPNAME}"
VIAddVersionKey "FileDescription" "${APPNAME} installer"
VIAddVersionKey "FileVersion"     "${VERSION}"
VIAddVersionKey "ProductVersion"  "${VERSION}"
VIAddVersionKey "CompanyName"     "${PUBLISHER}"
VIAddVersionKey "LegalCopyright"  "${PUBLISHER}"

; ---------- The pages people click through ----------
!define MUI_ICON   "${ICON}"
!define MUI_UNICON "${ICON}"
!define MUI_ABORTWARNING
!define MUI_WELCOMEFINISHPAGE_BITMAP   "${ART}/welcome.bmp"
!define MUI_UNWELCOMEFINISHPAGE_BITMAP "${ART}/welcome.bmp"
!define MUI_HEADERIMAGE
!define MUI_HEADERIMAGE_RIGHT
!define MUI_HEADERIMAGE_BITMAP         "${ART}/header.bmp"
!define MUI_HEADERIMAGE_UNBITMAP       "${ART}/header.bmp"

!define MUI_WELCOMEPAGE_TITLE "Install Churchie ${VERSION}"
!define MUI_WELCOMEPAGE_TEXT "Churchie puts song lyrics, Bible verses and pictures on your church's projector.$\r$\n$\r$\nIf Churchie is already installed, this updates it. Your songs, settings and schedules are kept.$\r$\n$\r$\nClick Next to continue."
!insertmacro MUI_PAGE_WELCOME
!insertmacro MUI_PAGE_DIRECTORY
!insertmacro MUI_PAGE_INSTFILES

; Two ticked boxes on the last page: open Churchie, and put a shortcut on the desktop.
; (NSIS only offers one "run" and one "readme" box, so the readme box makes the shortcut.)
!define MUI_FINISHPAGE_TITLE "Churchie is installed"
!define MUI_FINISHPAGE_TEXT "You'll find Churchie in the Start menu.$\r$\n$\r$\nNew to Churchie? See the guide at churchie.netlify.app"
!define MUI_FINISHPAGE_RUN "$INSTDIR\Churchie.exe"
!define MUI_FINISHPAGE_RUN_TEXT "Open Churchie now"
!define MUI_FINISHPAGE_SHOWREADME ""
!define MUI_FINISHPAGE_SHOWREADME_TEXT "Put a Churchie shortcut on the desktop"
!define MUI_FINISHPAGE_SHOWREADME_FUNCTION CreateDesktopShortcut
!insertmacro MUI_PAGE_FINISH

!insertmacro MUI_UNPAGE_CONFIRM
!insertmacro MUI_UNPAGE_INSTFILES

!insertmacro MUI_LANGUAGE "English"

; ---------- Checks before anything happens ----------
Function .onInit
  ${IfNot} ${RunningX64}
    MessageBox MB_ICONSTOP "Churchie needs 64-bit Windows 10 or 11."
    Abort
  ${EndIf}
FunctionEnd

; ---------- Installing ----------
Section "Churchie" SecMain
  SectionIn RO
  SetOutPath "$INSTDIR"
  ; If Churchie is open, Windows won't let its file be replaced: the installer then
  ; shows a "Retry" box, and closing Churchie and clicking Retry carries on.
  File /r "${SRC}/*.*"

  WriteUninstaller "$INSTDIR\${UNINSTEXE}"
  CreateShortcut "$SMPROGRAMS\Churchie.lnk" "$INSTDIR\Churchie.exe"
  WriteRegStr HKCU "Software\Churchie" "InstallDir" "$INSTDIR"

  ; So Churchie appears in Settings > Apps, where it can be uninstalled.
  WriteRegStr   HKCU "${UNINSTKEY}" "DisplayName"          "${APPNAME}"
  WriteRegStr   HKCU "${UNINSTKEY}" "DisplayVersion"       "${VERSION}"
  WriteRegStr   HKCU "${UNINSTKEY}" "Publisher"            "${PUBLISHER}"
  WriteRegStr   HKCU "${UNINSTKEY}" "URLInfoAbout"         "${WEBSITE}"
  WriteRegStr   HKCU "${UNINSTKEY}" "DisplayIcon"          "$INSTDIR\Churchie.exe"
  WriteRegStr   HKCU "${UNINSTKEY}" "InstallLocation"      "$INSTDIR"
  WriteRegStr   HKCU "${UNINSTKEY}" "UninstallString"      '"$INSTDIR\${UNINSTEXE}"'
  WriteRegStr   HKCU "${UNINSTKEY}" "QuietUninstallString" '"$INSTDIR\${UNINSTEXE}" /S'
  WriteRegDWORD HKCU "${UNINSTKEY}" "NoModify" 1
  WriteRegDWORD HKCU "${UNINSTKEY}" "NoRepair" 1
  ${GetSize} "$INSTDIR" "/S=0K" $0 $1 $2
  WriteRegDWORD HKCU "${UNINSTKEY}" "EstimatedSize" $0
SectionEnd

Function CreateDesktopShortcut
  CreateShortcut "$DESKTOP\Churchie.lnk" "$INSTDIR\Churchie.exe"
FunctionEnd

; ---------- Uninstalling ----------
; Only the files this installer put there are removed (listed in UNLIST), never the whole
; folder, in case someone chose a folder that has other things in it.
Section "Uninstall"
  Delete "$SMPROGRAMS\Churchie.lnk"
  Delete "$DESKTOP\Churchie.lnk"
  !include "${UNLIST}"
  Delete "$INSTDIR\${UNINSTEXE}"
  RMDir "$INSTDIR"
  DeleteRegKey HKCU "${UNINSTKEY}"
  DeleteRegKey HKCU "Software\Churchie"
SectionEnd
