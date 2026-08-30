# S-CPU Assembler

## Overview

The **S-CPU Assembler** translates human-readable assembly (and convenient pseudo-instructions) into executable machine code for the **S-CPU**.
It comes as both a **.NET library** and a **command-line tool**, supporting multiple output formats for simulators, FPGA ROMs, breadboard builds, and quick uploads through **S-Link**.

**Recommended reading:** if you are new to the project, start with the
[**S-CPU Architecture guide**](../../docs/architecture.md) for the processor
fundamentals, instruction semantics, flags, memory map, and timing model.

## Project Structure

* **SCPU.Assembler.Core**
  .NET 10 library implementing parsing, macro expansion, banking, encoding, and emitters. Can be reused (e.g., in the [C# simulator](../simulator/) or [S-Code compiler](../compiler/)).

* **SCPU.Assembler.CLI**
  Command-line app wrapping the core library for day-to-day assembly tasks.

## Quick start

Each example shows both supported workflows: the `scpu-assembler` launcher
from a Release and the equivalent command from a source checkout.

### Assemble and print an annotated listing

```sh
# Release
./scpu-assembler ./samples/asm/BlinkLED.asm -p

# Source checkout
dotnet run --project ./software/assembler/SCPU.Assembler.CLI -- ./samples/asm/BlinkLED.asm -p
```

### Assemble to binary and write a file

```sh
# Release
./scpu-assembler -f Binary -o ./blink.bin ./samples/asm/BlinkLED.asm

# Source checkout
dotnet run --project ./software/assembler/SCPU.Assembler.CLI -- -f Binary -o ./blink.bin ./samples/asm/BlinkLED.asm
```

### Assemble for Logisim/Digital

```sh
# Release
./scpu-assembler -d FREQ_HZ=50_000 -f Logisim16 -o ./hardware/digital/rom.hex ./samples/asm/BlinkLED.asm

# Source checkout
dotnet run --project ./software/assembler/SCPU.Assembler.CLI -- -d FREQ_HZ=50_000 -f Logisim16 -o ./hardware/digital/rom.hex ./samples/asm/BlinkLED.asm
```

### Assemble for Gowin (with a constant)

```sh
# Release
./scpu-assembler -d FREQ_HZ=81_000_000 -f Gowin -o ./hardware/gowin/src/gowin_dpb/rom.mi ./samples/asm/BlinkLED.asm

# Source checkout
dotnet run --project ./software/assembler/SCPU.Assembler.CLI -- -d FREQ_HZ=81_000_000 -f Gowin -o ./hardware/gowin/src/gowin_dpb/rom.mi ./samples/asm/BlinkLED.asm
```

### Assemble and POST directly to S-Link

```sh
# Release
./scpu-assembler -d FREQ_HZ=2_000_000 -f Binary -u http://slink.local/upload ./samples/asm/BlinkLED.asm

# Source checkout
dotnet run --project ./software/assembler/SCPU.Assembler.CLI -- -d FREQ_HZ=2_000_000 -f Binary -u http://slink.local/upload ./samples/asm/BlinkLED.asm
```

Sends the produced binary to [**S-Link**](../../firmware/slink/) to flash the ROM quickly.

## CLI Usage

```sh
Usage:
  ./scpu-assembler <file> [options]

Arguments:
  <file>  File to assemble

Options:
  -?, -h, --help                    Show help and usage information
  --version                         Show version information
  -o, --output <path>               Write output to the specified file
  -f, --format <format>             The output format [default: Annotated]
  -d, --define <KEY[=VALUE]>        Define constants. Usage: -d KEY or -d KEY=VALUE. If value is omitted, it defaults to 'true'.
  -p, --print                       Print the output to the console
  -q, --quiet                       Suppress console logging output
  -u, --post <URL>                  POST the assembled payload to the specified URL (e.g. http://slink.local/upload)
```

### Output formats

* **Annotated** — Human-readable annotated listing (aligned table) for debugging and inspection.
* **Binary** — Raw binary image of the assembled program (big-endian 16-bit words, MSB then LSB).
* **IntelHex** — Intel HEX formatted output for programmers/loaders that expect .hex files.
* **Logisim16** — Word-oriented hex dump compatible with Logisim's memory initialization.
* **Verilog** — One 16-bit word per line in hex (e.g., for `$readmemh` in HDL testbenches).
* **Gowin** — ROM initialization format compatible with Gowin IPs (e.g., `Gowin_DPB`).
* **Symbol** — Plain-text symbol table listing all constants and labels (`NAME=0xXXXX` per line).

## Instruction and Macro Reference

The S-CPU exposes only four native instructions: `NOR`, `ADD`, `STA`, and `JCC`. Every other mnemonic documented below is an assembler macro that expands recursively into these four instructions.

### Conventions

| Term | Meaning |
|---|---|
| `A` | 16-bit accumulator. |
| `C` | Carry flag (`CF`). For subtraction macros, `C = 1` represents a borrow. |
| `operand` | Immediate value, direct memory operand, or indirect memory operand accepted by the assembler. |
| `address` | Destination address or jump target. |
| `MAX_VALUE` | `0xFFFF`, declared in `software/assembler/SCPU.Assembler.Core/Resources/Bootloader.asm`. |
| Native words | Number of native instructions emitted after recursively expanding all nested macros. Labels do not consume words. Extended operands and long jumps may add ROM constants or rewrite operands later in the assembly pipeline. |
| Clobber | Reserved CPU register whose previous value is not preserved. `A` and `C` are described separately as outputs. |

Unless explicitly stated otherwise:

- arithmetic wraps modulo `2^16`;
- `NOR` preserves `C`;
- `ADD` replaces `C` with its unsigned carry-out;
- `STA` preserves `A` and `C` for normal RAM and MMIO destinations; writing to `CF` in reserved MMIO device #7 sets `C`;
- `JCC` tests `C`, jumps when `C = 0`, and clears `C` after the test;

Only `R0` through `R9` are intended as general-purpose registers for application code. Other predefined CPU registers are reserved for the architecture, calling convention, stack handling, or internal macro expansion. Registers such as `RPAR`, `RPEEK`, and `RRET` may be overwritten by macros; `SP`, `FP`, `CF`, and `RSINK` also have dedicated architectural roles.

> [!NOTE]
> The native-word counts below describe code size after macro expansion. Conditional paths may execute fewer instructions at runtime. In particular, `JCS` and `JMP` emit two `JCC` instructions even though one path may execute only the first one.

## Native instructions

### Summary

| Instruction | Operation | Result in `A` | Effect on `C` | Native words |
|---|---|---|---|---:|
| `NOR operand` | Bitwise NOR | `A = ~(A \| operand)` | Preserved | 1 |
| `ADD operand` | Unsigned addition | `A = A + operand` | Replaced by carry-out | 1 |
| `STA address` | Store accumulator | Unchanged | Preserved, except `STA CF` sets it | 1 |
| `JCC address` | Jump if carry clear | Unchanged | Cleared after the test | 1 |

### `NOR operand`

Performs a bitwise NOR between the accumulator and the selected operand.

```asm
nor {operand}
```

**Operation**

```text
A = ~(A | operand)
```

**Result**

- `A` receives the 16-bit NOR result.
- Memory is unchanged.

**Carry flag**

- Preserved.


**Native words:** 1

**Remarks**

`NOR` is functionally complete: all other Boolean operations can be reconstructed from it. It is also used by `CLR`, because `A NOR 0xFFFF` always produces zero.

---

### `ADD operand`

Adds the selected unsigned 16-bit operand to the accumulator.

```asm
add {operand}
```

**Operation**

```text
wide = A + operand
A    = wide & 0xFFFF
C    = wide > 0xFFFF
```

**Result**

- `A` receives the low 16 bits of the sum.

**Carry flag**

- Replaced by the unsigned carry-out of the addition.


**Native words:** 1

**Remarks**

`ADD` is also used to implement loading, increment, decrement, left shift, comparisons, and subtraction-related macros.

---

### `STA address`

Stores the accumulator at the selected destination address.

```asm
sta {address}
```

**Operation**

```text
memory[address] = A
```

**Result**

- The selected memory or MMIO destination receives `A`.
- `A` is unchanged.

**Carry flag**

- Preserved for normal RAM and MMIO destinations.
- `STA CF` is a special write-triggered MMIO operation that sets `C = 1`, regardless of the value in `A`.


**Native words:** 1

**Remarks**

`STA RSINK` has no externally visible effect and is used as the architectural `NOP`. `RSINK` must never be read because its read value is undefined.

---

### `JCC address`

Jumps when the carry flag is clear.

```asm
jcc {address}
```

**Operation**

```text
if C == 0:
    PC = address
C = 0
```

**Result**

- `A` is unchanged.
- The program counter either jumps to `address` or continues sequentially.

**Carry flag**

- Consumed and cleared after the test.


**Native words:** 1

**Remarks**

Because testing consumes the flag, macros such as `JCS`, `JMP`, `CLC`, `ADC`, and `SBC` must explicitly reconstruct any carry value that needs to survive.

## Macros

### Concept

Macros are **pseudo-instructions** that expand into one or more **native S-CPU instructions**.
They make the assembly syntax more expressive and compact, while the underlying execution still relies solely on the S-CPU's four fundamental operations: **`NOR`**, **`ADD`**, **`STA`**, and **`JCC`**.

All macros are defined in [`SCPU.Assembler.Core/Resources/Macros/`](SCPU.Assembler.Core/Resources/Macros/).

* Each macro can take parameters (e.g. `{operand}` or `{address}`) that are replaced at expansion time.
* Macros can **call other macros**, so expansions may be recursive.
* After all macros are expanded, the resulting native instructions go through the usual encoding, addressing, and optimization passes (including **Extended Operands** and **Long Jump** handling).

### Macro file format

Each macro is defined as a block:

```
[macro <name> {optional_params}]
<one or more lines of code>
```

Examples:

```
[macro clr]
nor MAX_VALUE

[macro lda {operand}]
clr
add {operand}
```

* `CLR` is a macro with no parameters, expanded to `NOR MAX_VALUE`.
  `MAX_VALUE` equals `0xFFFF`, so `NOR 0xFFFF` results in `A = 0`.
* `LDA {operand}` is a macro that calls another macro (`CLR`) and then adds the operand:
  → `LDA #1` expands to `NOR 0xFFFF` + `ADD #1`.

### Common and control-flow macros

| Macro | Native words | Final `A` | Final `C` | Clobbers |
|---|---:|---|---|---|
| `NOP` | 1 | Preserved | Preserved | None |
| `CLR` | 1 | `0` | Preserved | None |
| `LDA operand` | 2 | `operand` | Clear | None |
| `MOV dest, src` | 3 | `src` | Clear | None |
| `JZ address` | 2 | `A - 1` | Clear | None |
| `JNZ address` | 3 | `A - 1` | Clear | None |
| `JCS address` | 2 | Preserved | Clear | None |
| `JMP address` | 2 | Preserved | Clear | None |
| `HALT` | 2 | Preserved | Clear | None |
| `RST` | 2 | Preserved | Clear | None |

### Logic and bit-manipulation macros

| Macro | Native words | Final `A` | Final `C` | Clobbers |
|---|---:|---|---|---|
| `NOT` | 1 | `~A` | Preserved | None |
| `NOT operand` | 3 | `~operand` | Clear | None |
| `AND operand` | 6 | `A & operand` | Clear | `RPAR` |
| `NAND operand` | 7 | `~(A & operand)` | Clear | `RPAR` |
| `OR operand` | 2 | `A \| operand` | Preserved | None |
| `XOR operand` | 12 | `A ^ operand` | Clear | `RPAR`, `RPAR+1` |
| `LSL` | 2 | `A << 1` | Old bit 15 | `RPAR` |
| `LSL operand` | 3 | `operand << 1` | Old bit 15 of operand | None |
| `ROL` | 4 | Rotate left by one | Clear | `RPAR` |
| `ROL operand` | 6 | Rotate operand left by one | Clear | `RPAR` |
| `ROR` | 60 | Rotate right by one | Clear | `RPAR` |
| `ROR operand` | 62 | Rotate operand right by one | Clear | `RPAR` |
| `LSR` | 66 | Logical shift right by one | Clear | `RPAR` |
| `LSR operand` | 68 | Logical shift operand right by one | Clear | `RPAR` |

### Arithmetic and carry macros

| Macro | Native words | Final `A` | Final `C` | Clobbers |
|---|---:|---|---|---|
| `INC` | 1 | `A + 1` | Carry-out | None |
| `INC operand` | 3 | `operand + 1` | Carry-out | None |
| `DEC` | 1 | `A - 1` | `1` when original `A != 0` | None |
| `DEC operand` | 3 | `operand - 1` | `1` when operand `!= 0` | None |
| `NEG` | 2 | `-A` | `1` only when original `A = 0` | None |
| `NEG operand` | 4 | `-operand` | `1` only when operand `= 0` | None |
| `SUB operand` | 3 | `A - operand` | Borrow (`1` when `A < operand`) | None |
| `ADC operand` | 19 | `A + operand + C_in` | Exact carry-out | `RPAR` |
| `SBC operand` | 20 | `A - operand - C_in` | Exact borrow-out | `RPAR` |
| `LDC operand` | 3 | `operand` | Preserved | None |
| `CLC` | 1 | Preserved | Clear | None |
| `SEC` | 1 | Preserved | Set | None |

### Stack and subroutine macros

| Macro | Native words | Final `A` | Final `C` | Clobbers |
|---|---:|---|---|---|
| `LDS index` | 6 | `[SP + index]` | Clear | `RPEEK` |
| `LDS index, address` | 7 | `[SP + index]` | Clear | `RPEEK` |
| `STS index` | 8 | Preserved input value | Clear | `RPAR`, `RPEEK` |
| `STS index, operand` | 7 | `operand` | Clear | `RPEEK` |
| `POP` | 6 | Popped value | Clear | None |
| `POP address` | 7 | Popped value | Clear | None |
| `PUSH` | 5 | New `SP` | `1` when original `SP != 0` | None |
| `PUSH operand` | 7 | New `SP` | `1` when original `SP != 0` | None |
| `CALL address` | 9 | New `SP` | Clear | None |
| `RET` | 9 | Return address | Clear | `RRET` |

## Common and control-flow macros

### `NOP`

Performs no architectural operation.

```asm
[macro nop]
sta RSINK
```

**Native expansion**

```asm
sta RSINK
```

**Native words:** 1

**Result**

- `A` is preserved.
- `C` is preserved.
- RAM and normal peripherals are unchanged.


**Remarks**

`RSINK` is a write-only register in the internal S-CPU MMIO device. Writes are discarded. Reads are undefined and must not be used.

---

### `CLR`

Clears the accumulator.

```asm
[macro clr]
nor MAX_VALUE
```

**Native expansion**

```asm
nor 0xFFFF
```

**Native words:** 1

**Result:** `A = 0`.

**Carry flag:** preserved.


---

### `LDA operand`

Loads an immediate value or memory operand into the accumulator.

```asm
[macro lda {operand}]
clr
add {operand}
```

**Native expansion**

```asm
nor 0xFFFF
add {operand}
```

**Native words:** 2

**Result:** `A = operand`.

**Carry flag:** cleared, because adding any 16-bit operand to zero cannot overflow.


**Remarks**

Unlike a conventional native load instruction, `LDA` is synthesized through the ALU and therefore replaces the previous carry flag.

---

### `MOV dest, src`

Loads `src`, stores it at `dest`, and leaves the moved value in the accumulator.

```asm
[macro mov {dest}, {src}]
lda {src}
sta {dest}
```

**Native expansion**

```asm
nor 0xFFFF
add {src}
sta {dest}
```

**Native words:** 3

**Result**

```text
A       = src
[dest]  = src
```

**Carry flag:** cleared by `LDA`.


---

### `JZ address`

Jumps when the accumulator is zero.

```asm
[macro jz {address}]
add MAX_VALUE
jcc {address}
```

**Native expansion**

```asm
add 0xFFFF
jcc {address}
```

**Native words:** 2

**Condition:** the jump is taken when the input accumulator equals zero.

**Result**

- `A = A_input - 1` modulo `2^16`.
- The test is destructive.

**Carry flag:** consumed and clear after `JCC`.


**Remarks**

`A + 0xFFFF` produces no carry only when `A = 0`. For every non-zero input, it produces a carry.

---

### `JNZ address`

Jumps when the accumulator is not zero.

```asm
[macro jnz {address}]
add MAX_VALUE
jcs {address}
```

**Fully expanded native code**

```asm
add 0xFFFF
jcc $+2
jcc {address}
```

**Native words:** 3

**Condition:** the jump is taken when the input accumulator is non-zero.

**Result:** `A = A_input - 1` modulo `2^16`.

**Carry flag:** consumed and clear after the test.


**Remarks**

Like `JZ`, this is a destructive accumulator test.

---

### `JCS address`

Jumps when the carry flag is set.

```asm
[macro jcs {address}]
jcc $+2
jcc {address}
```

**Native expansion:** identical to the macro body.

**Native words:** 2

**Condition:** the jump is taken when the input `C = 1`.

**Result:** `A` is preserved.

**Carry flag:** consumed and clear after the macro.


**Remarks**

When `C = 0`, the first `JCC` skips the second instruction. When `C = 1`, the first `JCC` clears the flag without jumping, and the second `JCC` performs the requested jump.

---

### `JMP address`

Performs an unconditional jump regardless of the input carry flag.

```asm
[macro jmp {address}]
jcc {address}
jcc {address}
```

**Native expansion:** identical to the macro body.

**Native words:** 2

**Result:** `PC = address`; `A` is preserved.

**Carry flag:** clear after the jump.


**Remarks**

When `C = 0`, the first `JCC` jumps immediately. When `C = 1`, the first instruction clears the flag and the second one jumps.

---

### `HALT`

Stops useful execution by repeatedly jumping to itself.

```asm
[macro halt]
jmp $
```

**Fully expanded native code**

```asm
jcc $
jcc $
```

**Native words:** 2

**Result:** execution remains at the halt location.

**Carry flag:** clear after entering the loop.


**Remarks**

`$` is the assembler symbol for the current program address, so `JMP $` creates an infinite self-loop.

---

### `RST`

Jumps to address zero.

```asm
[macro rst]
jmp 0x0
```

**Fully expanded native code**

```asm
jcc 0x0
jcc 0x0
```

**Native words:** 2

**Result:** `PC = 0x0000`; `A` is preserved.

**Carry flag:** clear after the jump.


## Logic and bit-manipulation macros

### `NOT`

Inverts every bit of the accumulator.

```asm
[macro not]
nor #0
```

**Native words:** 1

**Result:** `A = ~A_input`.

**Carry flag:** preserved.


---

### `NOT operand`

Loads and inverts an operand.

```asm
[macro not {operand}]
lda {operand}
not
```

**Fully expanded native code**

```asm
nor 0xFFFF
add {operand}
nor #0
```

**Native words:** 3

**Result:** `A = ~operand`.

**Carry flag:** clear, because the internal `LDA` replaces it with the carry-out of loading from zero.


---

### `AND operand`

Computes a bitwise AND using De Morgan's law.

```asm
[macro and {operand}]
not
sta RPAR
lda {operand}
not
nor RPAR
```

**Fully expanded native code**

```asm
nor #0
sta RPAR
nor 0xFFFF
add {operand}
nor #0
nor RPAR
```

**Native words:** 6

**Result:** `A = A_input & operand`.

**Carry flag:** clear. `LDA` clears it and the following `NOR` instructions preserve it.

**Clobbers:** `RPAR`.

**Remarks**

The operand must not alias `RPAR`, because the macro overwrites `RPAR` before loading the operand.

---

### `NAND operand`

Computes the complement of a bitwise AND.

```asm
[macro nand {operand}]
and {operand}
not
```

**Fully expanded native code**

```asm
nor #0
sta RPAR
nor 0xFFFF
add {operand}
nor #0
nor RPAR
nor #0
```

**Native words:** 7

**Result:** `A = ~(A_input & operand)`.

**Carry flag:** clear.

**Clobbers:** `RPAR`.

**Remarks:** the operand must not alias `RPAR`.

---

### `OR operand`

Computes a bitwise OR.

```asm
[macro or {operand}]
nor {operand}
not
```

**Fully expanded native code**

```asm
nor {operand}
nor #0
```

**Native words:** 2

**Result:** `A = A_input | operand`.

**Carry flag:** preserved.


---

### `XOR operand`

Computes a bitwise exclusive OR using a NOR-only Boolean construction.

```asm
[macro xor {operand}]
sta RPAR
nor {operand}
sta RPAR+1
lda RPAR
nor RPAR+1
sta RPAR
lda {operand}
nor RPAR+1
nor RPAR
not
```

**Fully expanded native code**

```asm
sta RPAR
nor {operand}
sta RPAR+1
nor 0xFFFF
add RPAR
nor RPAR+1
sta RPAR
nor 0xFFFF
add {operand}
nor RPAR+1
nor RPAR
nor #0
```

**Native words:** 12

**Result:** `A = A_input ^ operand`.

**Carry flag:** clear because both internal `LDA` operations clear it.

**Clobbers:** `RPAR`, `RPAR+1`.

**Remarks**

The operand must not alias `RPAR` or `RPAR+1`. This implementation uses a compact NOR-based construction to reduce code size. A more direct composition using `NAND`, `OR`, and `AND` would require 19 native words, while this version uses 12.

---

### `LSL`

Shifts the accumulator left by one bit.

```asm
[macro lsl]
sta RPAR
add RPAR
```

**Native expansion:** identical to the macro body.

**Native words:** 2

**Result:** `A = (A_input << 1) & 0xFFFF`.

**Carry flag:** receives the original bit 15.

**Clobbers:** `RPAR`.


**Remarks**

A one-bit logical left shift is equivalent to multiplying an unsigned 16-bit value by two. Since S-CPU already has `ADD`, the operation can therefore be implemented by adding the accumulator to itself.

---

### `LSL operand`

Loads an operand and shifts it left by one bit.

```asm
[macro lsl {operand}]
lda {operand}
add {operand}
```

**Fully expanded native code**

```asm
nor 0xFFFF
add {operand}
add {operand}
```

**Native words:** 3

**Result:** `A = (operand << 1) & 0xFFFF`.

**Carry flag:** receives the original bit 15 of the operand.


---

### `ROL`

Rotates the accumulator left by one bit.

```asm
[macro rol]
lsl
jcc $+2
inc
```

**Generated native code**

```asm
sta RPAR
add RPAR
jcc $+2
add #1
```

**Native words:** 4  
**Executed instructions:** 3 when the original bit 15 is clear, otherwise 4.

**Result**

```text
A = ((A_input << 1) | (A_input >> 15)) & 0xFFFF
```

**Carry flag:** clear.

**Clobbers:** `RPAR`.

**Remarks**

`LSL` places the original bit 15 in `C`. `JCC` consumes that flag: when it was set, execution continues to `INC`, which inserts a one into bit 0. The shifted value is always even and at most `0xFFFE`, so this final increment cannot overflow and the resulting carry is clear.

---

### `ROL operand`

Loads an operand and rotates it left by one bit.

```asm
[macro rol {operand}]
lda {operand}
rol
```

**Native words:** 6  
**Executed instructions:** 5 or 6.

**Result:** `A = ROL16(operand, 1)`.

**Carry flag:** clear.

**Clobbers:** `RPAR`.

---

### `ROR`

Rotates the accumulator right by one bit by applying fifteen left rotations.

```asm
[macro ror]
rol
; repeated 15 times
```

**Native words:** 60  
**Executed instructions:** between 45 and 60, depending on the bits rotated through position 15.

**Result:** `A = ROR16(A_input, 1)`.

**Carry flag:** clear.

**Clobbers:** `RPAR`.

**Remarks**

On a 16-bit word, rotating left fifteen times is equivalent to rotating right once. This allows `ROR` to be implemented entirely from `ROL`, at the cost of a relatively large 60-word expansion.

---

### `ROR operand`

Loads an operand and rotates it right by one bit.

```asm
[macro ror {operand}]
lda {operand}
ror
```

**Native words:** 62  
**Executed instructions:** between 47 and 62.

**Result:** `A = ROR16(operand, 1)`.

**Carry flag:** clear.

**Clobbers:** `RPAR`.

---

### `LSR`

Performs a logical right shift by rotating right and clearing the new most-significant bit.

```asm
[macro lsr]
ror
and #0x7FFF
```

**Native words:** 66  
**Executed instructions:** between 51 and 66.

**Result:** `A = A_input >> 1`.

**Carry flag:** clear.

**Clobbers:** `RPAR`.

**Remarks**

S-CPU has no native right-shift instruction. `LSR` is implemented by rotating right once and then clearing the new most-significant bit with `AND #0x7FFF`. This reuses the existing `ROR` macro, but expands to 66 native words.

---

### `LSR operand`

Loads an operand and performs a logical right shift.

```asm
[macro lsr {operand}]
lda {operand}
lsr
```

**Native words:** 68  
**Executed instructions:** between 53 and 68.

**Result:** `A = operand >> 1`.

**Carry flag:** clear.

**Clobbers:** `RPAR`.

## Arithmetic and carry macros

### `INC`

Increments the accumulator.

```asm
[macro inc]
add #1
```

**Native words:** 1

**Result:** `A = A_input + 1` modulo `2^16`.

**Carry flag:** set only when the input was `0xFFFF`.


---

### `INC operand`

Loads and increments an operand.

```asm
[macro inc {operand}]
lda {operand}
inc
```

**Fully expanded native code**

```asm
nor 0xFFFF
add {operand}
add #1
```

**Native words:** 3

**Result:** `A = operand + 1` modulo `2^16`.

**Carry flag:** set only when the operand was `0xFFFF`.


---

### `DEC`

Decrements the accumulator.

```asm
[macro dec]
add MAX_VALUE
```

**Native expansion**

```asm
add 0xFFFF
```

**Native words:** 1

**Result:** `A = A_input - 1` modulo `2^16`.

**Carry flag**

- `C = 0` when the original accumulator was zero.
- `C = 1` for every non-zero input.


---

### `DEC operand`

Loads and decrements an operand.

```asm
[macro dec {operand}]
lda {operand}
dec
```

**Native words:** 3

**Result:** `A = operand - 1` modulo `2^16`.

**Carry flag:** `C = 1` when the operand was non-zero, otherwise `C = 0`.


---

### `NEG`

Computes the two's-complement negation of the accumulator.

```asm
[macro neg]
not
add #1
```

**Fully expanded native code**

```asm
nor #0
add #1
```

**Native words:** 2

**Result:** `A = -A_input` modulo `2^16`.

**Carry flag:** set only when the input was zero.


---

### `NEG operand`

Loads and negates an operand.

```asm
[macro neg {operand}]
lda {operand}
neg
```

**Native words:** 4

**Result:** `A = -operand` modulo `2^16`.

**Carry flag:** set only when the operand was zero.


---

### `SUB operand`

Subtracts an operand from the accumulator.

```asm
[macro sub {operand}]
not
add {operand}
not
```

**Fully expanded native code**

```asm
nor #0
add {operand}
nor #0
```

**Native words:** 3

**Result:** `A = A_input - operand` modulo `2^16`.

**Carry flag:** represents borrow: `C = 1` when `A_input < operand`, otherwise `C = 0`.


**Remarks**

This convention is the opposite of architectures where carry means “no borrow”. `SBC` follows the S-CPU convention `C = borrow`.

---

### `ADC operand`

Adds an operand and the input carry, while preserving the exact output carry.

```asm
[macro adc {operand}]
jcc __adc_add_{uid}
inc
jcc __adc_add_{uid}
lda {operand}
sta RPAR
jmp __adc_set_{uid}
__adc_add_{uid}:
add {operand}
sta RPAR
jcs __adc_set_{uid}
lda RPAR
jmp __adc_end_{uid}
__adc_set_{uid}:
lda RPAR
sec
__adc_end_{uid}:
```

**Native words:** 19

**Result**

```text
wide = A_input + operand + C_input
A    = wide & 0xFFFF
C    = wide > 0xFFFF
```

**Carry flag:** exact unsigned carry-out.

**Clobbers:** `RPAR`.

**Remarks**

The macro handles the special case where adding the input carry wraps `0xFFFF` to `0x0000`. It saves the result before testing or restoring `C`, because `JCC` consumes the flag and `LDA` clears it.

The operand may alias `RPAR`: every path reads the operand before storing the final result in `RPAR`.

---

### `SBC operand`

Subtracts an operand and the input borrow, while preserving the exact output borrow.

```asm
[macro sbc {operand}]
jcc __sbc_sub_{uid}
dec
jcc __sbc_wrap_{uid}
__sbc_sub_{uid}:
sub {operand}
sta RPAR
jcs __sbc_set_{uid}
lda RPAR
jmp __sbc_end_{uid}
__sbc_wrap_{uid}:
sub {operand}
sta RPAR
__sbc_set_{uid}:
lda RPAR
sec
__sbc_end_{uid}:
```

**Native words:** 20

**Result**

```text
signed = A_input - operand - C_input
A      = signed modulo 65536
C      = signed < 0
```

**Carry flag:** exact borrow-out, using the S-CPU convention `C = borrow`.

**Clobbers:** `RPAR`.

**Remarks**

The explicit wrap path handles `A_input = 0x0000` with an input borrow of one. Without it, the intermediate `DEC` borrow would be lost when `SUB` updates `C`.

The operand may alias `RPAR`, because it is read before the result is stored there.

---

### `LDC operand`

Loads an operand while preserving the carry flag.

```asm
[macro ldc {operand}]
clr
nor {operand}
not
```

**Fully expanded native code**

```asm
nor 0xFFFF
nor {operand}
nor #0
```

**Native words:** 3

**Result:** `A = operand`.

**Carry flag:** preserved, because the macro uses only `NOR` instructions.


**Remarks**

This is intentionally different from `LDA`, which clears `C` through its internal `ADD`.

---

### `CLC`

Clears the carry flag while preserving the accumulator.

```asm
[macro clc]
jcc $+1
```

**Native words:** 1

**Result:** `A` is preserved; `C = 0`.


**Remarks**

`$+1` is the next instruction, so `JCC $+1` always continues sequentially while consuming and clearing the Carry Flag.

---

### `SEC`

Sets the carry flag while preserving the accumulator.

```asm
[macro sec]
sta CF
```

**Native words:** 1

**Result:** `A` is preserved; `C = 1`.


**Remarks**

`CF` is a write-triggered register in the internal S-CPU MMIO device. Any write sets the flag, regardless of the value in `A`.

## Stack and subroutine macros

The stack grows downward. `SP` points to the next free word below the current top of stack:

```text
PUSH: [SP] = value, then SP = SP - 1
POP:  SP = SP + 1, then value = [SP]
```

Consequently, stack-relative indexes are one-based:

- `index = 1` addresses the current top of stack;
- `index = 2` addresses the next older value;
- `index = 0` addresses the free word currently pointed to by `SP`.

### `LDS index`

Loads a stack-relative value.

```asm
[macro lds {index}]
lda SP
add {index}
sta RPEEK
lda @(RPEEK)
```

**Fully expanded native code**

```asm
nor 0xFFFF
add SP
add {index}
sta RPEEK
nor 0xFFFF
add @(RPEEK)
```

**Native words:** 6

**Result:** `A = [SP + index]`.

**Carry flag:** clear after the final `LDA`.

**Clobbers:** `RPEEK`.

---

### `LDS index, address`

Loads a stack-relative value and stores it at an address.

```asm
[macro lds {index}, {address}]
lds {index}
sta {address}
```

**Native words:** 7

**Result**

```text
A         = [SP + index]
[address] = A
```

**Carry flag:** clear.

**Clobbers:** `RPEEK`.

---

### `STS index`

Stores the input accumulator at a stack-relative index and restores the value in `A`.

```asm
[macro sts {index}]
sta RPAR
lda SP
add {index}
sta RPEEK
lda RPAR
sta @(RPEEK)
```

**Native words:** 8

**Result**

```text
[SP + index] = A_input
A            = A_input
```

**Carry flag:** clear after `LDA RPAR`.

**Clobbers:** `RPAR`, `RPEEK`.

**Remarks**

The index must not alias `RPAR`, because the macro stores the input accumulator in `RPAR` before calculating the stack address.

---

### `STS index, operand`

Stores an operand at a stack-relative index.

```asm
[macro sts {index}, {operand}]
lda SP
add {index}
sta RPEEK
lda {operand}
sta @(RPEEK)
```

**Native words:** 7

**Result**

```text
A            = operand
[SP + index] = operand
```

**Carry flag:** clear.

**Clobbers:** `RPEEK`.

**Remarks**

The operand must not alias `RPEEK`, because the calculated target address overwrites `RPEEK` before the operand is loaded.

---

### `POP`

Removes the top stack word and loads it into the accumulator.

```asm
[macro pop]
inc SP
sta SP
lda @SP
```

**Fully expanded native code**

```asm
nor 0xFFFF
add SP
add #1
sta SP
nor 0xFFFF
add @SP
```

**Native words:** 6

**Result**

```text
SP = SP_input + 1
A  = [SP]
```

**Carry flag:** clear after the final `LDA`.


---

### `POP address`

Pops the top stack word and stores it at an address.

```asm
[macro pop {operand}]
pop
sta {operand}
```

**Native words:** 7

**Result**

```text
SP        = SP_input + 1
A         = [SP]
[operand] = A
```

**Carry flag:** clear.


**Remarks**

Despite the parameter name in the macro source, the argument is used as a destination address.

---

### `PUSH`

Pushes the accumulator onto the downward-growing stack.

```asm
[macro push]
sta @SP
dec SP
sta SP
```

**Fully expanded native code**

```asm
sta @SP
nor 0xFFFF
add SP
add 0xFFFF
sta SP
```

**Native words:** 5

**Result**

```text
[SP_input] = A_input
SP         = SP_input - 1
A          = SP
```

**Carry flag:** produced by the internal decrement: clear only when the original `SP` was zero, set otherwise.


**Remarks**

`PUSH` does not preserve the pushed value in `A`; it leaves the updated stack pointer in the accumulator.

---

### `PUSH operand`

Loads and pushes an operand.

```asm
[macro push {operand}]
lda {operand}
push
```

**Native words:** 7

**Result**

```text
[SP_input] = operand
SP         = SP_input - 1
A          = SP
```

**Carry flag:** produced by the stack-pointer decrement.


---

### `CALL address`

Pushes a generated return address and jumps to a subroutine.

```asm
[macro call {address}]
push #(__ret_{uid})
jmp {address}
__ret_{uid}:
```

**Native words:** 9

**Result**

- The return label address is pushed onto the stack.
- `SP` is decremented.
- Execution continues at `address`.
- `A` contains the updated `SP`.

**Carry flag:** clear after `JMP`.


**Remarks**

`{uid}` generates a unique return label for every macro expansion.

---

### `RET`

Pops a return address and jumps to it.

```asm
[macro ret]
pop RRET
jmp @(RRET)
```

**Native words:** 9

**Result**

```text
SP   = SP_input + 1
RRET = [SP]
A    = RRET
PC   = RRET
```

**Carry flag:** clear after `JMP`.

**Clobbers:** `RRET`.

## Reserved-register and aliasing rules

Only `R0` through `R9` are intended as general-purpose registers for application code. The other predefined CPU registers are reserved for the architecture, calling convention, stack handling, or internal macro expansion. They must not be used to retain program values across macro calls.

Several macros use reserved memory-mapped CPU registers as scratch storage:

| Register | Used by |
|---|---|
| `RPAR` | `AND`, `NAND`, `XOR`, `LSL`, `ROL`, `ROR`, `LSR`, `ADC`, `SBC`, `STS index` |
| `RPAR+1` | `XOR` |
| `RPEEK` | `LDS`, `STS` |
| `RRET` | `RET` |
| `CF` | `SEC` |
| `RSINK` | `NOP` |

A macro may overwrite every reserved register listed as a clobber. Application code must therefore never rely on the previous contents of `RPAR`, `RPAR+1`, `RPEEK`, `RRET`, or any other reserved scratch register. Use `R0` through `R9` for values that must survive macro execution.

## Assembly Language

### Comments & case

* `;` starts a comment until end of line.
* Mnemonics and directives are **case-insensitive**.

```asm
LDA #123      ; load literal 123 into A
```

### Literals

* **Decimal**: `123`, `2_000`
* **Binary**: `0b1010_1100`
* **Hex**: `0xBF86`

Underscores `_` are ignored and improve readability.

Example:

```asm
#bank userpage
tmp: #res 1

#bank prg
MOV tmp, #123     ; [tmp] ← 123
```

### Built-in variables

* **`$`** — current program counter.

```asm
JMP $          ; infinite loop (HALT pattern)
JCC $+2        ; skip next instruction if C=0
```

### Constants & preprocessing

`#const` defines constants (expressions allowed):

```asm
#const FREQ_HZ         = 2_000_000
#const CYCLES_PER_LOOP = 14
#const ITERS_PER_MS    = (FREQ_HZ / 1000) / CYCLES_PER_LOOP
```

`#include` pulls shared code:

```asm
#include "common/Delay.asm"
```

Conditional compilation with braces and `defined(NAME)`:

```asm
#if defined(FREQ_HZ) && FREQ_HZ > 1_000_000
{
    HALT
}
#elif !defined(FREQ_HZ)
{
    CLR
}
else
{
    LDA #1
}
```

### Addressing modes

* **Immediate `#`** — literal:

  ```asm
  LDA #123      ; A ← 123
  ```

* **Absolute** — memory at fixed address:

  ```asm
  LDA 123       ; A ← [123]
  ```

* **Indirect `@`** — pointer stored in memory:

  ```asm
  LDA @ptr      ; A ← [ [ptr] ]
  ```

Note: **Immediate of a label = address of the label**

```asm
#bank userpage
val_a: #res 1
ptr:   #res 1

#bank prg
MOV ptr, #(val_a)   ; [ptr] ← address of val_a
INC @ptr            ; increments val_a
```

### Labels (global & nested locals)

* **Global** labels end with `:`

```asm
loop:
DEC counter
JNZ loop
```

* **Local** labels start with `.` and can nest (`.`, `..`, `...`) inside their parent global.

  * `.local` → 1st-level local inside a global
  * `..nested` → 2nd-level local inside a 1st-level
  * `...deep` → 3rd-level inside a 2nd-level, and so on.

They can always be referenced fully-qualified (`global.local.nested`), or relatively from inside the same section.

```asm
abc:
.n1:
  ..n2:
    LDA ..n2
    LDA .n1
    LDA xyz.n1.n2

xyz:
.n1:
  ..n2:
    LDA ..n2
    LDA .n1
    LDA abc.n1.n2
```

### Data directives

Data directives embed raw data in memory. They come in three variants:

* **`#d16`** — 16-bit words
* **`#d32`** — 32-bit double words (two 16-bit words)
* **`#d`** — unsized; each element uses its intrinsic size (mix numbers/strings)

A label is required.

```asm
helloText:   #d   "Hello, World!", "\n", 0
fontData:    #d16 0x1234, 0x5678, 42
longValues:  #d32 0xDEAD_BEEF, 0x0000_F00D
```

Expressions are allowed:

```asm
ledPattern:       #d16 8, 4, 2, 1, 2, 4
ledPatternLength: #d16 ledPatternLength - ledPattern
```

Here, `ledPatternLength` will contain the computed length of the pattern array.

## Banks & RAM Reservations

Two logical banks exist in S-CPU assembly:

* **`#bank prg`** — Program ROM (code and constants)
* **`#bank userpage`** — User-accessible RAM (for variables, pointers, buffers)

The **userpage** represents the writable RAM space available to user code.
However, parts of the RAM address space are **reserved** by the architecture (addresses below are the **assembler's virtual addresses**):

| Address range     | Purpose                                                                                  |
| ----------------- | ---------------------------------------------------------------------------------------- |
| `0x12000–0x120FF` | **System stack area** — used implicitly by `PUSH`, `POP`, `CALL`, and `RET`.             |
| `0x12100–0x126FF` | **Userpage** — available for user variables via `#bank userpage`.                        |
| `0x12700–0x127FF` | **CPU registers** — memory-mapped internal registers (`R0–R9`, `SP`, `FP`, flags, etc.). |

These predefined addresses are exposed in the assembler as **default constants**, automatically injected from [`Constants.asm`](../../software/assembler/SCPU.Assembler.Core/Resources/Constants.asm). You can use them directly to reference system areas or registers.

Example:

```asm
#bank userpage
counter: #res 1
ptr:     #res 1

#bank prg
MOV ptr, #(counter)
INC @ptr
```

Here, `counter` and `ptr` are allocated in the userpage zone, while the system stack and CPU registers remain protected and reserved.

## Entry Point

The **entry point** marks the **first user instruction** executed after the bootloader completes its initialization.
It is defined by the special label **`ENTRY_POINT`** in the `#bank prg` section.

* If you explicitly define `ENTRY_POINT`, the bootloader will jump to that label once initialization is complete.
* If no `ENTRY_POINT` is defined, the assembler automatically injects one **right after the bootloader and the extra data region**, before the first user instruction.
* Defining multiple `ENTRY_POINT` labels results in a compilation error.

This ensures every assembled ROM has a **well-defined program start address**, even without explicit configuration.

**Example (explicit ENTRY_POINT):**

```asm
#bank prg
ENTRY_POINT:
  LDA #42
  STA 0x2000
  HALT
```

**Example (implicit ENTRY_POINT):**

```asm
#bank prg
LDA #42
STA 0x2000
HALT
```

In this case, the assembler implicitly inserts an `ENTRY_POINT` label right before the first instruction, and the bootloader performs a `JMP ENTRY_POINT` once initialization (stack, registers, flags) is complete.

## Memory Model & Addressing

S-CPU supports ROM, RAM, MMIO, Immediate, and Indirect addressing modes.
Their native encoding and hardware behavior are described in the
[**S-CPU Architecture guide**](../../docs/architecture.md).

From the assembler's point of view, the important distinction is that ROM,
RAM, and MMIO belong to separate native spaces, while source code uses a
**unified virtual address space** to reference them without ambiguity.

### Unified virtual space

The assembler exposes the following virtual map:

* **ROM:** `0x00000–0x0FFFF`
* **RAM:** `0x12000–0x127FF`
* **MMIO:** `0x12800–0x12FFF`

The RAM and MMIO ranges are assembler abstractions. Before encoding an
instruction, the assembler converts them to the corresponding native operand
representation.

For example:

* ROM `0x02000` remains ROM address `0x2000`;
* RAM `0x12000` becomes native operand `0x2000`;
* RAM `0x127FF` becomes native operand `0x27FF`;
* MMIO `0x12800` becomes native operand `0x2800`.

This virtual mapping avoids an ambiguity in the native encoding: `0x2000`, for
example, is both a valid ROM address and the native operand encoding for RAM
offset zero.

The CPU itself never manipulates these 17-bit virtual addresses. They exist only
in assembly source, symbols, and development tools.

## Extended Operands & Long Jump

A native S-CPU instruction cannot always encode a full 16-bit value directly:

* **Immediate mode** provides an 11-bit value (`0x0000–0x07FF`).
* **Direct ROM mode** can address the first 8,192 ROM words
  (`0x0000–0x1FFF`), each containing a full 16-bit value.

To keep assembly syntax independent of these limits, the assembler can place a
full 16-bit value in a **low-ROM constant pool** and rewrite the instruction to
source its operand from that ROM word.

This mechanism is used for both **Extended Operands** and **Long Jumps**.

### Extended operands

When an immediate value does not fit in 11 bits, the assembler stores the full
16-bit value in the constant pool and replaces the immediate operand with a
direct ROM operand.

```asm
; Source
LDA #0x1234

; Conceptual result
LDA const_0

...

const_0:
#d16 0x1234
```

`0x1234` cannot be encoded directly in the 11-bit immediate field.

Instead, `const_0` is placed below `0x2000`, where its address fits in the
13-bit direct ROM operand field. Reading `ROM[const_0]` then provides the full
16-bit value `0x1234`.

### Long jumps

Jump destinations follow a similar rule.

If a destination address fits in the 11-bit immediate field, the assembler
encodes it directly.

If the destination is greater than `0x07FF`, the assembler stores the full
16-bit address in the low-ROM constant pool and rewrites the `JCC` operand to
read that address from ROM.

```asm
; Source
JCC farRoutine

; Conceptual result
JCC const_0

...

const_0:
#d16 farRoutine
```

Here, `JCC const_0` reads the 16-bit value stored in `ROM[const_0]`. That value
is the address of `farRoutine` and becomes the new Program Counter when the
carry condition is satisfied.

The address of `const_0` itself needs only 13 bits because the constant pool is
kept below `0x2000`, while the value stored there can contain any 16-bit ROM
address from `0x0000` to `0xFFFF`.

### Decision rules

| Case                 | Condition       | Assembler action                                                     |
| -------------------- | --------------- | -------------------------------------------------------------------- |
| **Immediate value**  | `0x0000–0x07FF` | Encode directly using Immediate mode                                 |
|                      | `> 0x07FF`      | Store the 16-bit value in low ROM and rewrite the operand            |
| **Jump destination** | `0x0000–0x07FF` | Encode the destination directly                                      |
|                      | `> 0x07FF`      | Store the 16-bit destination in low ROM and rewrite the jump operand |

### ROM constant pool

The generated constants are inserted between the **bootloader** and
`ENTRY_POINT`:

```text
┌───────────────────────────────┐
│ Bootloader                    │
├───────────────────────────────┤
│ Constant pool                 │
│ - extended operands           │
│ - long-jump destinations      │  ← remains below 0x2000
├───────────────────────────────┤
│ User program (ENTRY_POINT →)  │
└───────────────────────────────┘
```

This provides several useful properties:

* **Always addressable:** every generated constant remains inside the 13-bit
  direct ROM window.
* **Transparent:** source code can use normal 16-bit constants and labels.
* **Reusable:** identical generated constants can share the same ROM word.
* **Inspectable:** annotated assembler output shows operands rewritten through
  the constant pool.

The assembler calculates and inserts these constants automatically. The internal
algorithm used to determine their final layout is described in
[Internal Architecture](#internal-architecture).

For the processor-side operand encoding and execution model, see the
[**S-CPU Architecture guide**](../../docs/architecture.md).

## Validation & Conformance

[`samples/asm/AutoTest.asm`](../../samples/asm/AutoTest.asm) is the common
conformance and regression program for S-CPU implementations.

It exercises the native instructions, addressing modes, assembler macros,
carry and borrow behavior, logic and shift operations, branches, and stack
operations. Macro results are checked against their expected accumulator and
carry-flag states, including representative 16-bit boundary cases.

The same assembled program can run in the CLI and desktop simulators, FPGA
implementations, and the physical S-CPU. A successful run turns on LED 0 and
halts. On failure, the hexadecimal display shows the error code defined near
the beginning of `AutoTest.asm`.

From the repository root:

```powershell
dotnet run --project ./software/simulator/SCPU.Simulator.CLI -- load ./samples/asm/AutoTest.asm -- run -- assert led = 1
```

The larger generated test suite in
[`AssemblerMacroConformanceTests.cs`](../simulator/SCPU.Simulator.Debugger.Tests/AssemblerMacroConformanceTests.cs)
complements AutoTest with exhaustive boundary combinations and deterministic
random inputs. It is intended for software regression testing and is not
embedded in the S-CPU ROM.

## Internal Architecture

This section is for developers working on the assembler source code.

### Assembly pipeline

`Parser.cs` runs first and covers: `#include`, `#const`, `#bank`, conditional preprocessing (`#if`/`#elif`/`#else`), macro expansion, and hierarchical label resolution. It produces a flat list of `Line` objects.

`Assembler.cs` then runs six stages on that list:

| Stage | Method | Purpose |
|---|---|---|
| 1 | `AllocateLabels` | Assign ROM word addresses to program labels; assign userpage RAM addresses to `#res` declarations |
| 2 | `ComputeRomAddresses` | Build a `Line -> ROM address` map, accounting for multi-word data directives (`#d32`) |
| 3 | `PatchInstructionsWithConstants` | Find which operands exceed their addressing-mode bit limit and rewrite them to use ROM constants (see below) |
| 4 | `ShiftLabelsForExtraData` | Shift all program labels at or after the bootloader boundary by the number of injected constants |
| 5 | `EmitFinalWords` | Encode all instructions to 16-bit words, inserting constant pool entries just before `ENTRY_POINT` |
| 6 | Flatten | Convert the word list to a big-endian byte array |

### Fixed-point algorithm for extended operands

Each constant injected into ROM shifts all following addresses by +1, which may push other operands past their 11-bit encoding limit, requiring more constants. This creates a chicken-and-egg problem: you need to know the final constant count to compute addresses, but you need addresses to know which operands need constants.

The naive solution is **iterative relaxation**: run stage 3 repeatedly until nothing changes. This works, but before the rewrite it was extremely slow (e.g., Pong.scode took 45 seconds to compile and assemble).

Instead, the assembler now uses a **fixed-point algorithm** that solves the problem upfront without iterative relaxations. For each constrained operand, it calculates the maximum number of constants it can absorb before its address overflows the 11-bit field. These tolerance values are sorted, then a single scan finds the exact total N where everything stabilizes at once. The implementation is in `CalculateThresholds` and `FindFixedPointN`.

Performance gain: Pong now compiles and assembles in under 3 seconds.

### Label hierarchy

Label nesting is resolved in `Parser.ComputeHierarchicalLabels`:

| Syntax | Resolved name | Note |
|---|---|---|
| `foo:` | `foo` | Global, resets the scope stack |
| `.bar:` | `foo.bar` | 1st-level local inside current global |
| `..baz:` | `foo.bar.baz` | 2nd-level local inside current 1st-level |
| `__name:` | `__name` | Transparent, not added to scope (used by macros) |

Sub-label references in operands (`.bar`, `..baz`) are resolved to their fully-qualified names at parse time, based on the current scope stack.

## Appendix

* See [`./samples/asm/`](../../samples/asm/) for runnable demos.
* See the simulator docs for running assembled code: [`../simulator/`](../simulator/).
* See S-Code to compile higher-level programs down to S-CPU assembly: [`../compiler/`](../compiler/).
