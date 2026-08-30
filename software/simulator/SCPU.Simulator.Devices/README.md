# S-CPU Simulator Devices

Reusable, UI-independent MMIO peripherals for S-CPU simulator frontends.

The project currently provides the buffered terminal and LED/display panel. Device events expose state changes without referencing Avalonia or Spectre.Console. Frontends decide how these events are rendered.

Diagnostic reads use `IODevice.Peek` so debugger inspection never consumes terminal input or otherwise changes device state.

## MMIO map

S-CPU MMIO starts at `0x12800`. Bits 10-8 select the device and the low byte selects its register.

### Device 0 — LED panel (`0x12800-0x128FF`)

| Address | Offset | Access | Function |
| --- | --- | --- | --- |
| `0x12801` | `0x01` | Read/write | 16-bit hexadecimal seven-segment display |
| `0x12802` | `0x02` | Read/write | LED output register; Desktop displays bits 7-0, TTL uses bits 3-0 |

The offsets are exposed by `LedPanelRegisters`.

### Device 1 — terminal (`0x12900-0x129FF`)

| Address | Offset | Access | Function |
| --- | --- | --- | --- |
| `0x12901` | `0x01` | Write | Append the low 7-bit ASCII character to terminal output |
| `0x12902` | `0x02` | Read | Remove and return the next buffered input character, or zero |
| `0x12903` | `0x03` | Read | Return 1 when input is available, otherwise zero |

The offsets are exposed by `TerminalRegisters`. Debugger inspection of `0x12902` uses `Peek` and does not remove the character.
