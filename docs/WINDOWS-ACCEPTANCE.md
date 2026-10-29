# Windows 11 acceptance checklist

Build validation in Linux: 10 automated checks passed, including actual loopback TCP sends, reconnect, no-client freeze, and occupied-port handling. Self-contained win-x64 publish succeeded. The Windows publish script also launches the packaged executable, checks that the main window appears, and closes it cleanly. Full interactive UI behavior, actual WasteWORKS capture, physical/virtual serial operation, and code signing still require manual verification.

1. Extract the portable ZIP and launch ScaleSim.exe on Windows 11 x64. Confirm window scaling, layout, keyboard input, and SIMULATION / TEST ONLY indicator.
2. Choose Automatic with default values and Start connection. From PowerShell run `./Test-Tcp.ps1` (provided beside the executable) to check 21 exact frames: ten at 20,000, ten at 19,980, then 19,960. Restart the automatic cycle before each test.
3. Pause near hold count 7/10. Confirm the counter and log stop; Resume must finish three identical sends before decrementing. The displayed counter can be checked more easily with a slower interval.
4. Close the TCP client and confirm progression freezes. Reconnect and confirm ten identical frames at the retained weight. Stop connection and start again to verify port release.
5. Set start 100, minimum 60, decrement 20, repeats 10; reset. Confirm 100 × 10, 80 × 10, 60 × 10, 100 × 10. Set minimum 65 and verify 100, 80, 100 without a below-minimum frame.
6. Check invalid manual values (-1, 1.5, 100000), invalid automatic minimum > start, and invalid counts/intervals. Error handling must preserve a usable app.
7. Test a busy TCP port and missing/busy COM port; resolve the conflict, stop and reconnect. Close while streaming and confirm a clean exit.
8. Reopen and verify preferences. Check the log remains bounded to 500 rows.

## WasteWORKS

Close other TCP testers so WasteWORKS is the only active client. Open Utilities → Socket Tester and connect to 127.0.0.1:10001. Check the continuous data, then close the tester and configure a test scale lane. Based on the sample: length including CR/LF 14, weight starts at position 5 (1-based), weight length 5, CR (ASCII 13) at position 13, LF at 14. Verify the actual parser with the WasteWORKS manual and test tools; these sample positions alone do not prove compatibility.

Apply Manual 20,000 lb and capture gross. Apply 12,000 lb and capture tare. Confirm 8,000 lb net. Use an isolated test configuration; ScaleSim itself does not modify WasteWORKS.

## Virtual COM release gate

Install and configure a trusted signed Windows 11 virtual-port driver using its vendor utility. Record vendor/version, Windows build, and the two verified COM endpoints. Use the simulator endpoint in ScaleSim and the partner in WasteWORKS Serial Tester at 9600/8/N/1 without flow control. Verify exact 14-byte frames and ten-send holds, then gross/tare capture through a real lane. Test busy ports, missing ports, stop/start, and app closure. Do not mark COM or WasteWORKS acceptance complete until these checks have actually run.
