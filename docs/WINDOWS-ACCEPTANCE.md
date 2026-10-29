# Windows 11 acceptance checklist

Build validation in Linux: 16 automated checks passed, including actual loopback TCP sends, reconnect, no-client freeze, and occupied-port handling. Self-contained win-x64 publish succeeded. The Windows publish script also launches the packaged executable, tests the dial/presets and invalid mode drafts, opens a real TCP connection through the UI, verifies a live dial change reaches the stream, checks stop cleanup, and captures Preview.png. Full interactive UI behavior, actual WasteWORKS capture, physical/virtual serial operation, and code signing still require manual verification.

1. Extract the portable ZIP and launch ScaleSim.exe on Windows 11 x64. Confirm window scaling, layout, keyboard input, and SIMULATION / TEST ONLY indicator.
2. Choose Automatic with default values and Start connection. From PowerShell run `./Test-Tcp.ps1` (provided beside the executable) to check 21 exact frames: ten at 20,000, ten at 19,980, then 19,960. Restart the automatic cycle before each test.
3. Pause near hold count 7/10. Confirm the counter and log stop; Resume must finish three identical sends before decrementing. The displayed counter can be checked more easily with a slower interval.
4. Close the TCP client and confirm progression freezes. Reconnect and confirm ten identical frames at the retained weight. Stop connection and start again to verify port release.
5. Set start 100, minimum 60, decrement 20, repeats 10; reset. Confirm 100 × 10, 80 × 10, 60 × 10, 100 × 10. Set minimum 65 and verify 100, 80, 100 without a below-minimum frame.
6. Check invalid manual values (-1, 1.5, 100000), invalid automatic minimum > start, and invalid counts/intervals. Error handling must preserve a usable app.
7. Test a busy TCP port and missing/busy COM port; resolve the conflict, stop and reconnect. Close while streaming and confirm a clean exit.
8. Drag the manual dial and verify that the selected weight reaches the next scheduled frame. Check zero and 99,999 endpoints, presets, ±20 buttons, keyboard arrows, page keys, and exact entry. Toggle Manual/Automatic with an invalid draft and confirm the selected mode still matches actual output.
9. During an automatic hold, change the interval and click Apply. The current weight and repeat count must be preserved. Disconnect a paused client at a 60-second interval and confirm a new client connects promptly.
10. Reopen and verify preferences, including Reduce motion and Show transmission log. Try closing with an invalid unapplied draft and confirm the next launch retains the last valid applied configuration. Check the log remains bounded to 500 rows.
11. Verify the truck follows positive/zero transmitted weights in both modes, freezes on Pause, and stays still with Reduce motion. Its illustration is not proof of a WasteWORKS capture. Check the layout at 100%, 125%, 150%, and 200% display scaling; smaller windows should scroll instead of losing controls.

## WasteWORKS

Close other TCP testers so WasteWORKS is the only active client. Open Utilities → Socket Tester and connect to 127.0.0.1:10001. Check the continuous data, then close the tester and configure a test scale lane. Based on the sample: length including CR/LF 14, weight starts at position 5 (1-based), weight length 5, CR (ASCII 13) at position 13, LF at 14. Verify the actual parser with the WasteWORKS manual and test tools; these sample positions alone do not prove compatibility.

Apply Manual 20,000 lb and capture gross. Apply 12,000 lb and capture tare. Confirm 8,000 lb net. Use an isolated test configuration; ScaleSim itself does not modify WasteWORKS.

## Virtual COM release gate

Install and configure a trusted signed Windows 11 virtual-port driver using its vendor utility. Record vendor/version, Windows build, and the two verified COM endpoints. Use the simulator endpoint in ScaleSim and the partner in WasteWORKS Serial Tester at 9600/8/N/1 without flow control. Verify exact 14-byte frames and ten-send holds, then gross/tare capture through a real lane. Test busy ports, missing ports, stop/start, and app closure. Do not mark COM or WasteWORKS acceptance complete until these checks have actually run.
