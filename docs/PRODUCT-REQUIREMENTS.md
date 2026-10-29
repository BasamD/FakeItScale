# ScaleSim — Product Requirements Document

**Platform:** Windows 11 (64-bit)
**Technology:** C# / .NET 10
**Purpose:** Simulate a physical truck-scale indicator for WasteWORKS testing.
**Priority:** Functionality, reliable transmissions, and accurate weight capture.

## 1. Product Objective

Build a Windows desktop application called **ScaleSim** that generates a continuous scale-indicator data feed without requiring a physical truck scale.

WasteWORKS must be able to connect to ScaleSim through either:

- **TCP/IP:** Default `127.0.0.1:10001`
- **Virtual COM:** A paired serial connection, such as COM10 → COM11

The application must support two independent testing modes:

1. **Manual Weight:** Enter any valid test weight and continuously transmit that same weight until the operator changes it.
2. **Automatic Weight:** Begin at a configured weight, transmit it at least 10 consecutive times, decrease it by a configured amount, and repeat until reaching the minimum weight before resetting.

**Critical requirement:** WasteWORKS may retry a scale capture when encountering an error. Therefore, the simulator must never change the weight after every individual transmission. It must provide a stable sequence of at least 10 identical transmissions before decreasing the value.

The final deliverable must be a functional Windows executable, not a UI prototype.

## 2. Scale Communication Protocol

Reproduce the following observed physical scale feed:

```text
[2]   33800LG [13][10]
[2]   33800LG [13][10]
[2]   33800LG [13][10]
```

The bracketed numbers represent actual ASCII control characters.

| Position | Content | Description |
|---|---|---|
| 1 | ASCII 2 | Start of Text (STX) |
| 2–4 | Three spaces | Leading padding |
| 5–9 | `33800` | Five-digit weight |
| 10–12 | `LG ` | Fixed literal suffix |
| 13 | ASCII 13 | Carriage Return |
| 14 | ASCII 10 | Line Feed |

### Required behavior

- Every transmission must be exactly **14 bytes**.
- Use actual STX, CR, and LF bytes, not printable `[2]`, `[13]`, or `[10]` strings.
- Preserve the three leading spaces and the `LG ` suffix.
- Weight values must contain exactly five numeric digits, zero-padded where necessary.
- Transmit plain ASCII without additional spaces, encoding markers, or separators.
- Supported weights: integer values from 0 through 99,999 pounds.
- Reject negative, decimal, or oversized weights rather than silently truncating them.

Example output:

```text
[2]   20000LG [13][10]
[2]   19980LG [13][10]
[2]   12000LG [13][10]
[2]   00000LG [13][10]
```

The exact hexadecimal representation of the 20,000-pound frame must be:

```text
02 20 20 20 32 30 30 30 30 4C 47 20 0D 0A
```

Use the same frame-generation function for TCP and serial transmission.

## 3. Manual Weight Mode

The operator must be able to enter a specific weight, click **Apply Weight**, and continuously broadcast that value.

Example:

- Enter `20000`.
- Click Apply Weight.
- ScaleSim repeatedly transmits 20,000 pounds.
- WasteWORKS captures the weight.
- Change the value to `12000`.
- ScaleSim immediately begins transmitting 12,000 pounds on the next scheduled frame.
- WasteWORKS captures the second weight.

This allows an inbound weight of 20,000 pounds and outbound weight of 12,000 pounds to produce an 8,000-pound net weight.

### Manual mode requirements

- Hold the chosen weight indefinitely.
- Never decrement automatically.
- Allow changes while actively streaming.
- Changes take effect on the next complete frame.
- Pause and resume must preserve the selected weight.
- Provide preset buttons for **20,000 lb**, **12,000 lb**, and **0 lb**.
- Allow any other valid integer weight to be entered manually.

## 4. Automatic Weight Mode

### Default configuration

| Setting | Default |
|---|---:|
| Starting weight | 20,000 lb |
| Minimum weight | 0 lb |
| Decrement | 20 lb |
| Transmission interval | 500 ms |
| Repeated sends per weight | **10** |
| After reaching minimum | Reset to 20,000 lb |
| Continuous output | Enabled |

### Required transmission logic

```text
SEND 01–10:  20000 lb
SEND 11–20:  19980 lb
SEND 21–30:  19960 lb
SEND 31–40:  19940 lb
SEND 41–50:  19920 lb
...
AT MINIMUM:  RESET TO 20000
REPEAT
```

All 10 transmissions at a particular weight must be identical.

Do not decrement until the current weight's 10th complete transmission has succeeded. The new weight must first appear in transmission 11, 21, 31, etc.

### Additional rules

- The repeat count must be configurable, with 10 as its minimum and default.
- Repeat count range: 10–1,000.
- Transmission interval must be configurable, defaulting to 500 milliseconds.
- A setting of 10 transmissions at 500 ms provides approximately five seconds at each weight.
- If a weight would decrement below the minimum, reset to the starting weight instead.
- Do not send negative weights.
- Pausing after, for example, seven transmissions must leave three identical transmissions before the next decrement.
- Starting a fresh automatic session resets to the configured starting weight and transmission count.
- Switching from Manual to Automatic must restart the configured automatic sequence.

**Important:** The simulator cannot directly detect WasteWORKS recapture errors without an additional communication mechanism. The minimum 10-transmission rule is intended to provide a stable capture window. Longer capture windows must be supported by increasing the repeat count or selecting Manual mode.

## 5. TCP/IP Mode — First Development Priority

Build a continuous raw TCP server.

**Default endpoint:** `127.0.0.1:10001`

### Functionality

- Start listening when the operator clicks Start.
- Allow WasteWORKS to connect as a TCP client.
- Continuously transmit the exact 14-byte scale frames.
- Begin transmission when a client connects.
- Support a configurable port.
- Default to localhost only.
- Provide an optional LAN listening mode, explicitly enabled by the operator.
- Display server status: Listening, Connected, Streaming, Paused, Disconnected, or Error.
- Support automatic client reconnection without restarting the application.
- Stop transmitting and freeze automatic weight progression when no TCP client is connected.
- Restart the current weight's 10-send counter when a new client connects.
- Handle an occupied network port without crashing.
- Do not automatically open Windows Firewall ports.

This must be a raw continuous TCP stream, not an HTTP server, REST API, JSON endpoint, or WebSocket connection.

## 6. Virtual COM Port Mode

Support a virtual serial-port pair so WasteWORKS can connect as though a physical RS232 scale indicator were attached.

Example:

- ScaleSim transmits through COM10.
- WasteWORKS receives through COM11.
- Both exist as Windows COM devices.

### Serial configuration

| Setting | Value |
|---|---|
| Baud rate | 9600 |
| Parity | None |
| Data bits | 8 |
| Stop bits | 1 |
| Flow control | None by default |

### Requirements

- Detect available serial ports.
- Allow selecting the simulator-side COM port.
- Display the corresponding WasteWORKS-side COM port.
- Support a Windows 11-compatible, signed virtual serial-port driver.
- Offer virtual COM pair creation through documented driver interfaces where supported.
- If a compatible driver is not installed, provide clear one-time setup instructions.
- Never silently install an untrusted driver or disable Windows security protections.
- Never overwrite existing physical COM ports.
- Handle disconnected, missing, or occupied COM ports gracefully.

A real virtual COM port requires a Windows driver. Do not simulate port creation through a fake dropdown or UI-only implementation.

COM mode must use the exact same weight algorithm and byte format as TCP/IP mode.

**Implementation order:** Complete and verify TCP/IP first. Add fully functional virtual COM support afterward.

## 7. Windows Application Interface

Create a simple, functional single-window desktop UI.

### Required controls

**Connection**

- TCP/IP / Virtual COM selector
- IP address and port, or COM port selector
- Start / Stop connection
- Connection status

**Weight Mode**

- Manual / Automatic selector
- Current simulated weight, prominently displayed

**Manual Controls**

- Enter weight
- Apply Weight
- Preset weight buttons

**Automatic Controls**

- Starting weight
- Minimum weight
- Decrement amount
- Transmission interval
- Number of identical sends per weight
- Reset Cycle

**Transmission Controls**

- Start
- Pause
- Resume
- Stop

**Live Monitor**

Display:

- Current weight
- Connected client/COM status
- Number of completed transmissions
- Current repeated-transmission count, such as `7 / 10`
- Latest transmitted frame
- Transmission timestamp
- Optional hexadecimal view
- Connection or transmission errors

Keep the transmission log bounded so it does not consume unlimited memory during long sessions.

Show a visible **SIMULATION / TEST ONLY** indicator.

Save preferences locally so the application remembers its configuration after closing.

## 8. WasteWORKS Compatibility

Use the supplied WasteWORKS Help Manual as the primary reference.

Relevant sections:

- Scales — pages 30–34
- Test Serial Devices — page 185
- Test Ethernet/IP Devices — page 186

WasteWORKS supports scale-indicator connections through a COM port or IP address, using continuous data output.

### Default sample frame positions

| Setting | Expected value |
|---|---:|
| Frame length including CR/LF | 14 bytes |
| Weight starting position | 5 (1-based) |
| Weight length | 5 |
| Character to check | ASCII 13 |
| CR position | 13 (1-based) |
| LF position | 14 (1-based) |

These values describe the sample's byte positions. Verify WasteWORKS' actual parsing behavior using its built-in scale testing feature rather than assuming the configuration will work without testing.

### Validation in WasteWORKS

**TCP/IP**

1. Start ScaleSim.
2. Select TCP/IP.
3. Use endpoint `127.0.0.1:10001`.
4. Open WasteWORKS Utilities → Socket Tester.
5. Verify incoming continuous scale data.
6. Configure and test the corresponding scale lane.
7. Confirm that WasteWORKS extracts the correct weight.

**Virtual COM**

1. Create or configure a compatible virtual COM pair.
2. Start ScaleSim on the simulator endpoint.
3. Configure WasteWORKS to use the receiving COM port.
4. Open WasteWORKS Utilities → Serial Tester.
5. Verify incoming data.
6. Test actual weight capture through the scale lane.

ScaleSim must never change WasteWORKS database records or production configurations.

## 9. Error Handling

The application must handle the following without freezing or crashing:

- TCP client disconnects unexpectedly.
- Connection is re-established.
- TCP listening port is already occupied.
- Virtual COM driver is missing.
- Selected COM port is unavailable or in use.
- Transmission encounters an I/O error.
- Weight input exceeds supported limits.
- Minimum weight exceeds starting weight.
- Decrement is zero or negative.
- Transmission interval is invalid.
- Repeat count is below 10.
- Application closes during streaming.

Do not count failed transmissions toward the required 10 repeated sends.

In COM mode, a successful serial write does not necessarily prove that WasteWORKS consumed the data. Report transmission success accurately without claiming client-side receipt.

## 10. Automated Tests and Acceptance Criteria

The following tests must pass.

| Test | Required result |
|---|---|
| Exact protocol | 14-byte frame matches sample |
| Initial auto output | 20,000 lb for transmissions 1–10 |
| First decrement | 19,980 lb begins at transmission 11 |
| Second decrement | 19,960 lb begins at transmission 21 |
| Manual weight | Remains unchanged across 100+ sends |
| Pause after send 7 | Resume completes remaining 3 sends |
| Minimum reset | Returns to starting weight correctly |
| TCP disconnect | Automatic progression freezes |
| TCP reconnect | Full stable repeat window resumes |
| Zero weight | Encoded as `00000` |
| Invalid weight | Rejected without corrupting output |
| Stop/restart | Resources released and session restarts cleanly |
| Serial vs TCP | Identical frame bytes |

Also test a smaller cycle using starting weight 100, minimum 60, decrement 20, and repeat count 10:

```text
100 × 10 frames
 80 × 10 frames
 60 × 10 frames
100 × 10 frames
...
```

### Manual end-to-end acceptance

The final application must support this workflow:

1. Launch ScaleSim on Windows 11.
2. Start the TCP listener.
3. Connect a TCP client and verify exact frame bytes.
4. Confirm 10 identical frames before the first decrease.
5. Open WasteWORKS and verify data in Socket Tester.
6. Set Manual Weight to 20,000 pounds.
7. Capture a gross weight.
8. Set Manual Weight to 12,000 pounds.
9. Capture a tare weight.
10. Verify the expected 8,000-pound net weight.

The COM release must additionally pass a real virtual-port-pair test.

Do not claim WasteWORKS integration has been verified unless the actual application was tested.

## 11. Architecture and Deliverables

Use C# with .NET 10 and WPF, or WinForms if that makes the implementation simpler and more reliable.

Separate these components:

- Scale frame encoder
- Weight generator and 10-send state machine
- TCP transport
- Serial transport
- Desktop UI
- Settings persistence
- Transmission logging

Use asynchronous I/O, proper cancellation, and graceful resource cleanup. Do not block the UI thread.

### Required deliverables

- Functional Windows 11 x64 `.exe`
- Complete C# source code
- Project/solution files
- Automated tests
- README with setup instructions
- WasteWORKS testing instructions
- Reproducible build and publish commands

Publish a self-contained Windows x64 executable so a normal workstation does not need to install the .NET runtime separately.

The first milestone is a complete, tested TCP/IP application. The second milestone adds genuine virtual COM-port integration.

If a required Windows driver or physical WasteWORKS test environment is unavailable, document that limitation precisely.

## 12. Final Instructions to Codex

**Implement ScaleSim, not merely a mockup or proposed architecture.**

Prioritize, in this order:

1. Exact 14-byte scale feed.
2. Manual fixed-weight transmission.
3. Automatic decrement after **10 identical successful transmissions**.
4. Automatic reset and looping.
5. Continuous TCP/IP transmission with reliable reconnection.
6. Usable Windows 11 desktop controls.
7. Virtual COM-port support.
8. Automated validation and executable packaging.

Correctness and repeatable WasteWORKS weight capture take precedence over animations, visual decoration, dashboards, or unrelated features.

Build, test, and deliver the application.
