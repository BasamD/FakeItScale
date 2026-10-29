# ScaleSim 1.2

This release fixes clipped controls, adds responsive resizing, and turns the truck illustration into a continuous feed-activity display.

- Sections grow to fit their controls. Connection and weight controls stack in a narrow window, keeping the same order. Smaller windows scroll vertically; Stop connection remains accessible.
- A smooth lineup of box trucks, tippers, and tankers moves while local frame writes are active, including zero-weight frames. Pause, Stop, disconnect, and stale writes stop the lineup in place. Reduce motion freezes the illustration without changing the feed.
- Frames written, last-write time/weight, byte count, and connection/write errors report real local events. Their tooltips explain what they measure. A write is not an acknowledgment from WasteWORKS.
- The automatic engine changes to the next weight only after its write succeeds. Failed boundary writes retain the prior weight, and reconnect gives that weight a full stable window. Duplicate/stale write tickets cannot inflate the engine's hold count.
- Serial startup now reports Connected before sending, so early writes/errors cannot be hidden by a late connection message.

## Run

Extract the downloaded ZIP to a folder and open ScaleSim.exe. Keep all extracted files together. No separate .NET runtime installation is required. Preferences from earlier versions are supported.

The TCP endpoint, exact 14-byte ASCII format, Manual mode, and ten-send automatic default remain compatible with the previous version. A virtual COM pair still requires a separately installed signed Windows driver.

## Validation

Core/TCP tests verify exact protocol bytes, successful-send counting, failed boundary writes, pause/resume, min/reset cycles, disconnect/reconnect, error reporting, timing changes, bounded monitoring, and freshness despite computer clock adjustments.

Windows UI checks exercise live Manual dial output over TCP, mode validation, Start/Stop cleanup, truck motion and freeze behavior, and control containment at four viewport widths with normal/150%/200% text sizes. These text-size checks supplement the manual Windows DPI checklist; they do not simulate every monitor or Windows display setting. Preview images are captured from the running Windows application.

Actual WasteWORKS capture and signed virtual COM driver compatibility must still be checked in the user's installation. ScaleSim does not modify WasteWORKS configuration or database records.
