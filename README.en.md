# UmcVolume

Windows volume control for Behringer USB audio interfaces. It consists of keyboard volume keys (Volume +, Volume −, Mute), an on-screen volume indicator and a settings window.

*Türkçe sürüm: `README.md`.*

## The problem

The UMC202HD driver reports to Windows that it implements endpoint volume in hardware. Windows therefore does not apply the volume itself and leaves it to the driver, but the driver does not apply it either. As a result, the Windows volume slider and the keyboard volume keys have no effect on a UMC output. Measured values:

| Property | Value |
|---|---|
| `IAudioEndpointVolume::QueryHardwareSupport` | `0x3` (hardware volume + hardware mute) |
| `GetVolumeRange` | −127 … 0 dB, 1 dB step, 51 steps |
| Driver | BEHRINGER UMC 202HD 192k, 5.72.0.19646 (based on Thesycon TUSBAUDIO), 2024-04-12 |

**Suggested permanent fix (driver side):** either the driver actually applies the endpoint volume it advertises, or it stops advertising hardware volume support. In the second case Windows applies the volume in software by itself and no extra tool is needed.

## Supported devices

Every audio output with Behringer's USB vendor ID (`VID_1397`) is detected automatically. The ID is read from each output's hardware information in Windows (`PKEY_Device_InstanceId`). All of these products use the same driver stack (Thesycon TUSBAUDIO) and may have the same problem.

| Model | Picture | Status |
|---|---|---|
| U-PHORIA UMC202HD | `assets\umc202hd.png` | Tested on real hardware |
| U-PHORIA UMC204HD | `assets\umc204hd.png` | Name and UI test; same issue reported on forums |
| U-PHORIA UMC404HD | `assets\umc404hd.png` | Name and UI test |
| U-PHORIA UMC1820 | `assets\umc1820.png` | Name and UI test |
| U-PHORIA UMC22 | `assets\umc22.png` | Name and UI test |
| Other Behringer USB devices (UMC202/204 96k, X AIR, etc.) | generic icon | Name and UI test |

If several Behringer devices are connected, the controlled device is chosen in this order: default output, most recently used device, first device found. When control moves from one device to another, the applications on the previous device are restored to 100%. For a non-Behringer device, part of the device name can be set in `DeviceMatch` in `settings.ini`.

To add a picture for a new model, save a PNG with a transparent background as `assets\<model>.png`. For example, `BEHRINGER UMC 404HD 192k` uses `umc404hd.png`.

## How it works

UmcVolume does not touch the audio driver or any audio processing plug-ins (APO, VST). Instead it uses the Windows **per-application session volumes**: it sets every application playing on the UMC output to the same level. These levels are applied in software by the Windows audio engine, so they are independent of the driver's behaviour.

- When a new application starts playing, Windows sends a notification and the application is adjusted immediately. As an extra safeguard, a check runs every second.
- On exit (`/exit` or Exit in the menu), all applications are restored to 100%.
- The volume curve is similar to Windows': halving the percentage halves the perceived loudness. Values: 100% = 0 dB, 50% = −10 dB, 25% = −20 dB. The signal is never raised above the source level, so there is no clipping.
- Keyboard keys are captured only while the default output is a UMC. With any other output selected, the keys go to Windows as usual.
- Applications using ASIO or WASAPI exclusive mode (DAWs) are not affected.

## Known limitations

- **Volume Mixer:** in the Windows Volume Mixer, all application sliders move together. UmcVolume overwrites changes made to individual applications with its own level.
- **Exclusive fullscreen:** the on-screen indicator is an always-on-top window. It cannot be drawn over games running in true exclusive fullscreen (no ordinary window can). The volume still changes. Borderless/windowed fullscreen works.
- **Windows shell UI:** the Start menu, Quick Settings, Alt+Tab and UAC prompts sit in a higher z-order band than always-on-top windows and can cover the indicator.
- **Elevated windows:** UmcVolume runs without elevation (`asInvoker`) and uses a low-level keyboard hook. Windows does not deliver keys to it while an elevated (administrator) window has focus, so volume keys may not work there. This has not been tested.

## Usage

- **Build:** run `build.cmd`. Only the .NET Framework 4.x compiler that ships with Windows is needed.
- **Run:**
  - `UmcVolume.exe` opens the window.
  - `UmcVolume.exe /tray` starts in the system tray without a window (used at Windows startup).
  - `UmcVolume.exe /exit` cleanly closes the running instance (for installers and uninstallers).
- **Window:** volume slider (drag, mouse wheel, arrow keys), mute button, step per key press (`0.5% / 1% / 2% / 5%`), list of controlled applications and a start-with-Windows switch.
- **Data folder:** `%APPDATA%\UmcVolume\` (`settings.ini`, `state.ini`, `umcvolume.log`). Nothing is written to the application folder, so it can be installed under `Program Files`.
- **Language:** Turkish and English. The Windows display language is used by default. `Language=tr` or `Language=en` in `settings.ini` fixes it. All strings are in `src\Strings.cs`.
- **High DPI:** the application declares itself DPI-aware (`app.manifest`). All sizes scale with the display, so it stays sharp at 125% or 150%. `UiScale=1.5` in `settings.ini` simulates 150% for testing.
- **Test:** `build-test.cmd [folder]` checks that model names are parsed correctly from known driver names and saves each model's device card as a PNG (`tools\DeviceArtTest.cs`).
- **Icon and version:** `assets\app.ico` is generated by `build.cmd` with `tools\MakeIcon.cs` if missing. Version information is in `src\AssemblyInfo.cs` (1.0.0).

## Signature and verification

`UmcVolume.exe` carries an Authenticode signature (SHA-256, RFC 3161 timestamp) made with the author's **self-signed** certificate. Because no certificate authority vouches for this certificate, Windows shows an "Unknown publisher" warning when the exe is opened. The signature proves that the file has not been modified since it was signed and that it was signed with the author's certificate.

**Verification** (PowerShell):

```powershell
Get-AuthenticodeSignature .\UmcVolume.exe | Format-List Status, SignerCertificate
(Get-AuthenticodeSignature .\UmcVolume.exe).SignerCertificate.Thumbprint
```

- Status is `UnknownError` or `NotTrusted`, which is expected for a self-signed certificate. `HashMismatch` means the file was modified.
- The thumbprint must match `cert\UmcVolume-signing.cer` and the value the author sent separately.
- SHA-256 hashes of every file in the package are listed in `SHA256SUMS.txt`.

**Rebuilding:** the complete source code is included. `build.cmd` builds it without any extra tools. Without `cert\thumbprint.txt` the exe is built unsigned.

**Adopting it as an official patch:** change the Company and Copyright lines in `src\AssemblyInfo.cs` and re-sign the exe with Behringer's own code-signing certificate: `signtool sign /fd SHA256 /tr <timestamp server> /td SHA256 ...`. The `cert\` folder and the `tools\sign*.ps1` scripts are then not needed.

**Author scripts:**

| Command | Purpose |
|---|---|
| `tools\sign-setup.ps1 -Name "Full Name"` | One-time: creates the certificate (`Cert:\CurrentUser\My`, RSA 3072, 5 years), exports its public part to `cert\`, writes the name into `src\AssemblyInfo.cs`. |
| `build.cmd` | Builds; signs with `tools\sign.ps1` when a certificate is set up. |
| `tools\package.ps1` | Creates `dist\UmcVolume-<version>.zip`: signed exe, source, public certificate, `SHA256SUMS.txt`. |

PowerShell scripts are run with `powershell -NoProfile -ExecutionPolicy Bypass -File <script>`. The private key stays in the author's Windows certificate store and is not part of the package.

## Dependencies and licences

| Component | Status |
|---|---|
| Application code (`src\`, `tools\`, scripts) | MIT License, © 2026 M.Burak Altiparmak (`LICENSE`). No third-party code. |
| .NET Framework 4.x, WinForms, Core Audio API | Part of Windows. Not redistributed. |
| Segoe UI / Segoe Fluent Icons fonts | Windows system fonts. Not redistributed. |
| `assets\umc*.png` | Official Behringer product images (behringer.com). Rights belong to Behringer / Music Tribe; not covered by the MIT License. |
| ~~Equalizer APO~~ | **Removed.** (GPL v2) |
| ~~LoudMax limiter~~ | **Removed.** Its licence (*"freeware and not commercial in any mean. You may not distribute and/or modify LoudMax without having an explicit permission from me."*) does not allow redistribution. |
