@echo off
rem Builds and runs tools\DeviceArtTest.cs (model-name parsing + device card rendering).
rem Usage: build-test.cmd [output folder for the PNGs]
setlocal
set CSC=%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\csc.exe
set S=%~dp0src
set OUT=%~1
if "%OUT%"=="" set OUT=%TEMP%
"%CSC%" /nologo /codepage:65001 /target:exe /main:UmcVolume.DeviceArtTest ^
  /out:"%TEMP%\UmcVolumeDeviceArtTest.exe" /win32manifest:"%~dp0app.manifest" ^
  /r:System.Windows.Forms.dll /r:System.Drawing.dll ^
  "%~dp0tools\DeviceArtTest.cs" "%S%\AssemblyInfo.cs" "%S%\Program.cs" "%S%\TrayApp.cs" "%S%\Settings.cs" ^
  "%S%\Strings.cs" "%S%\Dpi.cs" "%S%\VolumeModel.cs" "%S%\SessionVolume.cs" "%S%\AudioDevices.cs" ^
  "%S%\KeyboardHook.cs" "%S%\OsdForm.cs" "%S%\MainForm.cs" "%S%\DeviceArt.cs" "%S%\VolumeSlider.cs" "%S%\Ui.cs" || exit /b 1
pushd "%~dp0"
"%TEMP%\UmcVolumeDeviceArtTest.exe" "%OUT%"
set RC=%ERRORLEVEL%
popd
exit /b %RC%
