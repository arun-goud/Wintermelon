# Wintermelon (dual / multi-monitor lock screen)

Makes the Windows 10/11 lock screen appear on **all** monitors instead of only
the primary one. This is a modernized rebuild (targeting **.NET 8**) of
[arun-goud/Wintermelon](https://github.com/arun-goud/Wintermelon).

## How it works

Windows only draws the lock screen on the primary monitor; other monitors go
black. Wintermelon works around this by switching the **display projection
mode** around the lock event:

- **On workstation lock:** save the positions of all open windows, then switch
  projection to **Duplicate (Clone)** — every monitor now mirrors the primary,
  so the lock screen shows on all of them.
- **On workstation unlock:** switch projection back to **Extend** and move the
  windows that were on non-primary monitors back to where they were (~3 s).

Two Windows **Task Scheduler** tasks (triggered by the native *workstation
lock* / *workstation unlock* session events) run the executable with `save` and
`restore` respectively.

> Note: This trigger approach uses Task Scheduler's `SessionStateChangeTrigger`,
> so — unlike the original — it does **not** modify your Security audit policy.

## Files

| File | Purpose |
|------|---------|
| `Program.cs` | The save/clone + extend/restore logic |
| `Wintermelon.csproj` | `net8.0-windows`, `WinExe`, WinForms enabled |
| `install_wintermelon.ps1` | Builds the exe and registers the two tasks |
| `uninstall_wintermelon.ps1` | Removes the two tasks |

Runtime data: window positions are stored at
`%LOCALAPPDATA%\Wintermelon\winpos.txt`.

## Requirements

- Windows 10 or 11, two or more monitors
- .NET SDK 8.0+ (to build) / .NET Desktop Runtime 8.0 (to run)
- An enabled lock screen (Settings → Personalization → Lock screen). In
  Windows 11, also enable "Show the lock screen background picture on the
  sign-in screen" for the picture to appear on the secondary monitor.

## Install

Open **PowerShell as Administrator**, then:

```powershell
cd C:\Users\10157\Wintermelon
powershell -ExecutionPolicy Bypass -File .\install_wintermelon.ps1
```

This publishes `Wintermelon.exe` and registers both scheduled tasks. (Pass
`-SkipBuild` if the exe is already built.)

### Test

Press **Win+L**. The lock screen should now appear on every monitor. Sign back
in and your windows return to their original positions.

## Uninstall

From an elevated PowerShell:

```powershell
powershell -ExecutionPolicy Bypass -File .\uninstall_wintermelon.ps1
```

## Build only (no install)

```powershell
dotnet publish .\Wintermelon.csproj -c Release -r win-x64 --self-contained false -p:PublishSingleFile=true
```

Output: `bin\Release\net8.0-windows\win-x64\publish\Wintermelon.exe`

## Manual run (for testing)

```powershell
.\bin\Release\net8.0-windows\win-x64\publish\Wintermelon.exe save     # clone displays now
.\bin\Release\net8.0-windows\win-x64\publish\Wintermelon.exe restore  # extend + restore windows
```

## Credits

Original concept and code: [arun-goud/Wintermelon](https://github.com/arun-goud/Wintermelon) (GPL-2.0).
