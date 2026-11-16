# Audio Switcher

A small Windows tray app that keeps your default **output** and **input** audio devices set according to a priority list.

When a device is connected or disconnected, the app picks the highest-priority device that is available and makes it the default (console, multimedia and communications roles). If you switch devices by hand, the app leaves your choice alone until the set of connected devices changes again.

## Features

- Separate priority lists for output (speakers/headphones) and input (microphones). Reorder, add and remove devices from the UI.
- Switch now: double-click a device, or use the tray menu.
- Bluetooth panel: lists paired audio devices with their real link state, and has **Connect** / **Disconnect** buttons.
- **HyperX Cloud III Wireless** support. The USB dongle keeps the audio device "present" even when the headset is switched off. The app asks the dongle over HID whether the headset is actually linked, and skips it when it is off.
- Starts with Windows (optional) and runs from the tray.
- Single small `.exe` with no dependencies beyond .NET Framework 4.x, which ships with Windows.

## Build

No SDK needed. The C# compiler that ships with Windows is enough:

```powershell
powershell -ExecutionPolicy Bypass -File app\build.ps1
```

This produces `AudioSwitcher.exe` in the repository root. Run it once. It creates `config.json` from `config.example.json` on first start.

## Configuration

`config.json` (next to the exe) is edited by the UI and can also be edited by hand. The app reloads it automatically.

```json
{
  "pollSeconds": 3,
  "autoSwitch": true,
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
