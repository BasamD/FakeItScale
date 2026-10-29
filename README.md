# ScaleSim 1.1

Windows 11 x64 desktop scale-indicator simulator, built with C# / .NET 10 and WinForms. **SIMULATION / TEST ONLY.**

## Download and run

Extract `ScaleSim-Windows-x64.zip` to a folder on your Windows 11 PC and double-click `ScaleSim.exe`. This is a self-contained application: no .NET runtime installation is required. No administrator rights are required for localhost TCP operation. The build is unsigned; Windows may show an unknown-publisher warning. The Windows build workflow launches the packaged executable and checks the visible window, mode validation, live dial TCP output, and connection start/stop. The ZIP includes `Preview.png`, a screenshot captured on Windows. Complete the manual acceptance checks below before relying on WasteWORKS or virtual COM behavior.

The ZIP is portable, not an installer. Preferences are stored in `%LOCALAPPDATA%\ScaleSim\settings.json`. No WasteWORKS database records or settings are modified by ScaleSim. Deleting the extracted folder removes the app; deleting that preferences folder removes saved settings.

## TCP quick start

1. Leave TCP/IP selected, port `10001`, and **Allow LAN** unchecked. It listens on `127.0.0.1`. Start connection.
2. Connect WasteWORKS Socket Tester to `127.0.0.1:10001`. Only one client streams at a time; close the tester before connecting a scale lane.
3. In **2 Set the weight**, choose Manual. Drag the weight dial for live changes, use the 20,000 / 12,000 / Zero presets, or type any integer weight and click Apply. Focus the dial and use arrow keys for 20 lb steps, Page Up/Down for 1,000 lb, or Home/End for the endpoints.
4. Select Automatic to restart at 20,000 lb. Defaults send ten identical frames at 500 ms before subtracting 20 lb. Apply and restart validates and applies automatic settings and restarts the sequence. Changes to numeric controls alone do not change an active sequence.
5. In **3 Stream**, Pause preserves the weight and counter; Resume continues. Stop output resets the session and pauses; Start fresh resets and resumes. Stop connection releases the listener or serial port.

The frame interval applies to both modes (10–60,000 ms). Set it in **1 Connection** and click Apply to update a running sequence without resetting the weight or hold count. Starting a connection also applies the displayed interval. Automatic sends per weight accept 10–1,000. A new TCP client gets a fresh full hold window at the current weight. Progress freezes without an active TCP client. A complete write means bytes were accepted by the local transport; TCP and serial writes do not prove WasteWORKS captured them.

**Allow LAN** explicitly binds to all IPv4 interfaces. Use the Windows PC's LAN address in the remote client. No firewall rule is created automatically. Do not expose this unauthenticated test feed on an untrusted network.

## Exact protocol

One frame is 14 plain ASCII bytes: STX, three spaces, five zero-padded weight digits, `LG `, CR, LF.

20,000 lb: `02 20 20 20 32 30 30 30 30 4C 47 20 0D 0A`.

TCP is a raw byte stream, not HTTP. Reads can split or combine frames; clients must assemble frames. Negative, decimal, and greater-than-99,999 weights are rejected. The log and pending UI events are bounded to 500 entries; statistics count every completed write. Show transmission log opens the optional log; Hex bytes changes the format of newly logged frames.

## Virtual COM

Serial transport uses real Windows COM devices at 9600 baud, 8 data bits, no parity, one stop bit, and no flow control. Install a trusted vendor-supported Windows 11-compatible **signed** virtual null-modem driver separately, following its official instructions; then create an unused pair such as COM10/COM11. Verify both endpoints in Device Manager. Select COM10 in ScaleSim and configure WasteWORKS for COM11. The WasteWORKS COM field is an operator-supplied reference, not automatic pair discovery.

ScaleSim does not bundle, install, or create driver devices. Never disable driver signature enforcement or overwrite physical ports. Windows' standard serial enumeration cannot determine virtual pair relationships. Pair creation remains a vendor utility operation because no particular driver or management API was supplied. Real pair operation and driver compatibility have **not** been verified here. Missing/busy ports report errors; stop and reconnect after resolving them.

## Build and automated checks

Install the SDK pinned in `global.json`, then from the repository root:

```powershell
dotnet run --project tests/ScaleSim.Tests -c Release
dotnet publish src/ScaleSim.Desktop -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -o artifacts/win-x64
```

On Windows, run `scripts/Publish.ps1` to build, launch-test the packaged window, and create the portable Windows ZIP. Tests use a dependency-free executable harness: a nonzero exit code signals failure. They exercise exact encoding, invalid input, 10-send progression, manual holds, pause/resume, cycle reset, failed-write accounting, reconnection, actual TCP output, and listener resource cleanup. The Windows desktop target can be cross-compiled from Linux with `EnableWindowsTargeting`; the UI and COM port operations require Windows to run.

## A calmer testing screen

The current simulated weight stays prominent. The dashboard separates session frames written, last transmitted weight/time, error count, and connection state. Automatic mode shows the current stable-send count and a progress bar. Only the selected mode and transport controls are shown. Inline messages explain rejected values while preserving the running mode.

The truck illustration follows the **last transmitted** weight, in both modes: it moves onto the scale for positive weights and leaves at zero. Pause freezes it. Reduce motion removes movement and the send pulse. Animation never controls the transmission clock or claims a WasteWORKS capture acknowledgment.

See [What's new in 1.1](docs/RELEASE-1.1.md). Saved preferences from 1.0.1 remain readable; only valid applied weight settings are saved, so an invalid draft cannot break next startup. Each new connection starts a fresh statistics session. Manual weight changes and automatic cycle resets do not erase the transport's session statistics.

See [Windows acceptance](docs/WINDOWS-ACCEPTANCE.md) for remaining release validation. No WasteWORKS integration is claimed as verified; its help manual was referenced by the supplied requirements but was not uploaded.
