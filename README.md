# Audio Switcher

A small Windows tray app that keeps your default **output** and **input** audio devices set according to a priority list.

When a device is connected or disconnected, the app picks the highest-priority device that is available and makes it the default (console, multimedia and communications roles).

If you switch devices by hand, **Keep my manual choice** (on by default) decides what happens on the next device change:

- **On:** your choice stays. The app switches only if the device you picked disappears, or a newly connected device has a higher priority than it.
- **Off:** any device change re-applies the full priority list.

Editing the priority list, turning Auto switch back on and starting the app always re-apply the full list.

## Features

- Separate priority lists for output (speakers/headphones) and input (microphones). Reorder, add and remove devices from the UI.
- Switch now: double-click a device, or use the tray menu.
- Bluetooth panel: lists paired audio devices with their real link state, and has **Connect** / **Disconnect** buttons.
- **HyperX Cloud III Wireless** support. The USB dongle keeps the audio device "present" even when the headset is switched off. The app asks the dongle over HID whether the headset is actually linked, and skips it when it is off.
- English, Azerbaijani and Russian UI. English is the default, and the language can be switched in the window or the tray menu.
- Starts with Windows (optional) and runs from the tray.
- Single small `.exe` with no dependencies beyond .NET Framework 4.x, which ships with Windows.

## Install

Run `AudioSwitcher-Setup-<version>.exe`. It installs for the current user only, so no admin rights are needed. The installer:

- installs to `%LOCALAPPDATA%\Programs\Audio Switcher`
- adds a Start Menu shortcut, and optionally a desktop icon and "Start with Windows"
- appears in **Settings → Apps**, where it can be uninstalled

Settings and the log are stored in `%APPDATA%\AudioSwitcher`, so upgrades keep them. When you uninstall, the uninstaller asks whether to delete them.

## Build

No SDK needed. The C# compiler that ships with Windows is enough:

```powershell
powershell -ExecutionPolicy Bypass -File app\build.ps1
```

This produces `AudioSwitcher.exe` in the repository root.

To build the installer, install [Inno Setup 6](https://jrsoftware.org/isinfo.php) (`winget install JRSoftware.InnoSetup`) and run:

```powershell
powershell -ExecutionPolicy Bypass -File installer\build.ps1
```

The installer is written to `dist\`.

If a `config.json` sits next to the exe, the app runs in portable mode and keeps its settings there. Otherwise it uses `%APPDATA%\AudioSwitcher`. On first start it creates `config.json` from `config.example.json`.

## Configuration

`config.json` (next to the exe) is edited by the UI and can also be edited by hand. The app reloads it automatically.

```json
{
  "language": "en",
  "pollSeconds": 3,
  "autoSwitch": true,
  "keepManual": true,
  "output": ["HyperX Cloud III", "JBL Clip 4", "Speakers (Realtek"],
  "input":  ["HyperX Cloud III", "Microphone Array"]
}
```

Each entry is matched case-insensitively as a substring of the Windows device name. The first entry that matches a connected device wins.

## How it works

| Part | API |
|---|---|
| Device list and change events | Core Audio `IMMDeviceEnumerator` / `IMMNotificationClient` |
| Setting the default device | `IPolicyConfig` (undocumented, used by most switchers) |
| Bluetooth device list and link state | `BluetoothFindFirstDevice` (bthprops) |
| Bluetooth disconnect | `IOCTL_BTH_DISCONNECT_DEVICE`, which also works with Intel Bluetooth audio offload |
| Bluetooth connect | `KSPROPSETID_BtAudio` / `KSPROPERTY_ONESHOT_RECONNECT` on the device's KS filter |
| HyperX headset state | HID vendor report `0x66 0x82` on usage page `0xFF13` |

## Credits

The HyperX Cloud III Wireless HID protocol is from [LennardKittner/HyperHeadset](https://github.com/LennardKittner/HyperHeadset).
