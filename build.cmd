@echo off
setlocal
set CSC=%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\csc.exe
set S=%~dp0src
set ICON=%~dp0assets\app.ico

rem Generate the app icon once (tools\MakeIcon.cs) if it is missing.
if not exist "%ICON%" (
  "%CSC%" /nologo /out:"%TEMP%\UmcVolumeMakeIcon.exe" /r:System.Drawing.dll "%~dp0tools\MakeIcon.cs" || exit /b 1
  "%TEMP%\UmcVolumeMakeIcon.exe" "%ICON%" || exit /b 1
)

"%CSC%" /nologo /codepage:65001 /target:winexe /optimize+ /platform:anycpu ^
  /out:"%~dp0UmcVolume.exe" /win32icon:"%ICON%" /win32manifest:"%~dp0app.manifest" ^
  /r:System.Windows.Forms.dll /r:System.Drawing.dll ^
  "%S%\AssemblyInfo.cs" "%S%\Program.cs" "%S%\TrayApp.cs" "%S%\Settings.cs" "%S%\Strings.cs" "%S%\Dpi.cs" ^
  "%S%\VolumeModel.cs" "%S%\SessionVolume.cs" "%S%\AudioDevices.cs" "%S%\KeyboardHook.cs" ^
  "%S%\OsdForm.cs" "%S%\MainForm.cs" "%S%\DeviceArt.cs" "%S%\VolumeSlider.cs" "%S%\Ui.cs"
if errorlevel 1 (echo Derleme BASARISIZ & exit /b 1)
echo Derlendi: %~dp0UmcVolume.exe

rem Sign when a certificate has been set up (tools\sign-setup.ps1).
if exist "%~dp0cert\thumbprint.txt" (
  powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0tools\sign.ps1" -File "%~dp0UmcVolume.exe" || (echo Imzalama BASARISIZ & exit /b 1)
) else (
  echo Imzasiz derleme: cert\thumbprint.txt yok, bkz. tools\sign-setup.ps1
)
