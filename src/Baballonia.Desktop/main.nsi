;--------------------------------
; Includes

  !include "MUI2.nsh"
  !include "logiclib.nsh"

;--------------------------------
; Custom defines
  !define NAME "Baballonia"
  !define APPFILE "Baballonia.Desktop.exe"
  !define VERSION "1.1.0.5"
  !define SLUG "${NAME} v${VERSION}"

;--------------------------------
; General

  Name "${NAME}"
  OutFile "${NAME} Setup.exe"
  ; Default install directory in user's AppData folder
  InstallDir "$LOCALAPPDATA\${NAME}"
  InstallDirRegKey HKCU "Software\${NAME}" ""
  RequestExecutionLevel user

  SetCompressor /SOLID lzma ; "/FINAL" can be added to prevent anything from changing this later down the line.
  SetCompressorDictSize 32
  FileBufSize 64
  ManifestDPIAware true
  ;ManifestLongPathAware true ; disabled until the rest of the app is confirmed to have this setup right
  XPStyle on ; does this even do anything meaningful anymore?

;--------------------------------
; UI

  !define MUI_ICON "assets\IconOpaque.ico"
  !define MUI_HEADERIMAGE
  !define MUI_WELCOMEFINISHPAGE_BITMAP "assets\MUI_WELCOMEFINISHPAGE_BITMAP.bmp"
  !define MUI_HEADERIMAGE_BITMAP "assets\MUI_HEADERIMAGE_BITMAP.bmp"
  !define MUI_ABORTWARNING
  !define MUI_WELCOMEPAGE_TITLE "${SLUG} Setup"

;--------------------------------
; Pages

  ; Installer pages
  !insertmacro MUI_PAGE_WELCOME
  !insertmacro MUI_PAGE_LICENSE "assets\license.txt"
  !insertmacro MUI_PAGE_COMPONENTS
  !insertmacro MUI_PAGE_DIRECTORY
  !insertmacro MUI_PAGE_INSTFILES
  !insertmacro MUI_PAGE_FINISH

  ; Uninstaller pages
  !insertmacro MUI_UNPAGE_CONFIRM
  !insertmacro MUI_UNPAGE_INSTFILES

  ; Set UI language
  !insertmacro MUI_LANGUAGE "English"

;--------------------------------
; Section - Install App

  Section "-hidden app"
    SectionIn RO
    SetOutPath "$INSTDIR"

    ; Copy all files except Calibration, Firmware and runtime folders
    File /r /x "Calibration" /x "Firmware"

    ; Create Firmware directory
    CreateDirectory "$INSTDIR\Firmware"

    ; Copy Windows-only Firmware tooling
    CreateDirectory "$INSTDIR\Firmware\Windows"
    SetOutPath "$INSTDIR\Firmware\Windows"
    File /r "bin\Release\net8.0\win-x64\Firmware\Windows"

    ; Create Windows-only Calibration tooling
    CreateDirectory "$INSTDIR\Calibration"
    SetOutPath "$INSTDIR\Calibration"
    File /r "bin\Release\net8.0\win-x64\Calibration\Windows"

    ; Copy firmware over
    CreateDirectory "$INSTDIR\Firmware\Binaries"
    SetOutPath "$INSTDIR\Firmware\Binaries"
    File /r "bin\Release\net8.0\win-x64\Firmware\Binaries"

    ; Reset output path and write registry values
    SetOutPath "$INSTDIR"
    WriteRegStr HKCU "Software\${NAME}" "" $INSTDIR
    WriteUninstaller "$INSTDIR\Uninstall.exe"
  SectionEnd

;--------------------------------
; Section - Shortcut

  Section "Desktop Shortcut" DeskShort
    CreateShortCut "$DESKTOP\${NAME}.lnk" "$INSTDIR\${APPFILE}"
  SectionEnd

;--------------------------------
; Descriptions

  ;Language strings
  LangString DESC_DeskShort ${LANG_ENGLISH} "Create Shortcut on Dekstop."

  ;Assign language strings to sections
  !insertmacro MUI_FUNCTION_DESCRIPTION_BEGIN
  !insertmacro MUI_DESCRIPTION_TEXT ${DeskShort} $(DESC_DeskShort)
  !insertmacro MUI_FUNCTION_DESCRIPTION_END

;--------------------------------
; Remove empty parent directories

  Function un.RMDirUP
    !define RMDirUP '!insertmacro RMDirUPCall'

    !macro RMDirUPCall _PATH
          push '${_PATH}'
          Call un.RMDirUP
    !macroend

    ; $0 - current folder
    ClearErrors

    Exch $0
    ;DetailPrint "ASDF - $0\.."
    RMDir "$0\.."

    IfErrors Skip
    ${RMDirUP} "$0\.."
    Skip:

    Pop $0

  FunctionEnd

;--------------------------------
; Section - Uninstaller

Section "Uninstall"

  ;Delete Shortcut
  Delete "$DESKTOP\${NAME}.lnk"

  ;Delete Uninstall
  Delete "$INSTDIR\Uninstall.exe"

  ;Delete Folder
  RMDir /r "$INSTDIR"
  ${RMDirUP} "$INSTDIR"

  DeleteRegKey /ifempty HKCU "Software\${NAME}"

SectionEnd
