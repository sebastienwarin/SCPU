# S-CPU Architecture

S-CPU is a **minimalist 16-bit accumulator-based computer architecture** built
around only four native instructions.

The same architecture is implemented in software simulators, logic simulators,
FPGA, and physical TTL hardware. This document describes the common machine:
its instruction set, encoding, execution cycle, addressing modes, and memory
organization.

For project setup, tools, and implementation-specific documentation, start with
the [main README](../readme.md).

## Ecosystem Compatibility

All S-CPU implementations share the **same processor architecture, instruction
set, memory model, and ROM format**. An assembled program is therefore portable
between them without modification.

| Platform | Role |
| -------- | ---- |
| **[Simulator Desktop](../software/simulator/SCPU.Simulator.Desktop/README.md)** | Desktop frontend of the software simulator for fast execution and interactive debugging |
| **[Simulator CLI](../software/simulator/SCPU.Simulator.CLI/README.md)** | Command-line frontend of the software simulator for scripting, automation, and quick runs |
| **[Logisim](../hardware/logisim/README.md)** | Gate-level model exposing the processor datapath and control logic |
| **[Digital](../hardware/digital/README.md)** | 74xx chip-level prototype used to design and validate the physical TTL implementation |
| **[Icarus Verilog](../hardware/verilog/README.md)** | HDL simulation used for automated testing and signal-level validation |
| **[Gowin FPGA](../hardware/gowin/README.md)** | Synthesizable hardware implementation for the Tang Primer 25K |
| **[S-CPU TTL](../hardware/scpu-ttl/README.md)** | Physical implementation built from 74xx logic |

## Core Specifications

| Component | Description |
| --------- | ----------- |
| **Word size** | 16-bit |
| **ROM** | 65,536 × 16-bit words — 128 KiB of program storage |
| **RAM** | 2,048 × 16-bit words — 4 KiB for data and stack |
| **Memory-Mapped I/O (MMIO)** | 8 devices × 256 registers; device #7 is reserved by S-CPU |
| **Accumulator (A)** | 16-bit register used for arithmetic and logic operations |
| **Instruction Register (IR)** | 16-bit register holding the instruction currently being executed |
| **Program Counter (PC)** | 16-bit counter holding the ROM address of the next instruction |
| **Carry Flag (C)** | 1-bit flag used by arithmetic and conditional control flow |
| **Indirected Flag** | 1-bit flag indicating that an indirect operand has been resolved and the rewritten instruction is waiting to execute |
| **Step Counter** | 1-bit execution-state counter alternating between `S0` (fetch) and `S1` (execute) |

## Native Instruction Set

S-CPU has only **four native instructions**:

| Opcode | Mnemonic | Operation |
| ------ | -------- | --------- |
| `00` | `NOR operand` | `A = ~(A \| operand)` |
| `01` | `ADD operand` | `A = A + operand` |
| `10` | `STA address` | Store `A` in RAM or MMIO |
| `11` | `JCC address` | Jump to the address when the Carry Flag is clear |

Despite this very small instruction set, these four operations are sufficient
to construct the rest of the programming model.

`NOR` is functionally complete and can reconstruct all Boolean operations.
`ADD` provides arithmetic, `STA` writes results to memory or devices, and `JCC`
provides conditional control flow.

The assembler builds higher-level instructions as **macros**, recursively
expanding them until only native S-CPU instructions remain.

A few simple examples illustrate the idea:

* `CLR` is implemented with a single `NOR 0xFFFF`, which always produces zero.
* `LDA operand` combines `CLR` and `ADD operand` to load a value into the accumulator.
* `OR operand` is reconstructed from two `NOR` operations using De Morgan's law.
* `JMP address` is built from two consecutive `JCC` instructions so that the jump is taken regardless of the current Carry Flag.

Macros can also use other macros. For example, `MOV` is defined as an `LDA`
followed by `STA`. The assembler recursively expands the complete chain before
generating machine code.

This approach keeps the hardware instruction set extremely small while still
providing a practical and expressive assembly language.

The standard macro set includes:

* **Data movement:** `CLR`, `LDA`, `LDC`, `MOV`
* **Arithmetic:** `INC`, `DEC`, `NEG`, `SUB`, `ADC`, `SBC`
* **Logic:** `NOT`, `AND`, `NAND`, `OR`, `XOR`
* **Bit manipulation:** `LSL`, `LSR`, `ROL`, `ROR`
* **Carry control:** `CLC`, `SEC`
* **Flow control:** `JZ`, `JNZ`, `JCS`, `JMP`, `HALT`, `RST`
* **Stack operations:** `PUSH`, `POP`, `LDS`, `STS`
* **Subroutines:** `CALL`, `RET`

See the assembler's [**macro overview and reference**](../software/assembler/README.md#macros)
for expansions, native-word counts, flag effects, clobbered registers, and the
complete macro reference.

The macro definitions themselves are stored in
[`SCPU.Assembler.Core/Resources/Macros/`](../software/assembler/SCPU.Assembler.Core/Resources/Macros/).

## Instruction Encoding

Every native instruction is stored in a single **16-bit word**.

The two most significant bits select one of the four opcodes. The remaining
**14 bits describe the operand**:

```text
15          14 13                                           0
+--------------+---------------------------------------------+
| opcode (2)   | operand (14)                                |
+--------------+---------------------------------------------+
```

The operand field carries either a value or an address, and also defines
the **addressing mode** used to interpret it.

S-CPU supports five addressing modes:

* **ROM** — access a 16-bit word in program ROM.
* **RAM** — access a 16-bit word in data memory.
* **MMIO** — access a register exposed by an external device.
* **Immediate** — use a value stored directly in the instruction.
* **Indirect** — access an operand through a pointer stored in RAM.

Their binary encoding and detailed behavior are described below.

For RAM, MMIO, Immediate, and Indirect modes, bits 13–11 identify the mode,
leaving an **11-bit payload**:

```text
15      14 13      11 10                                  0
+----------+-----------+-----------------------------------+
| opcode   | mode (3)  | 11-bit payload                    |
+----------+-----------+-----------------------------------+
```

ROM is encoded slightly differently. When **bit 13 is `0`**, the remaining
13 operand bits form the ROM address:

```text
15      14 13 12                                          0
+----------+--+---------------------------------------------+
| opcode   | 0| 13-bit ROM address                          |
+----------+--+---------------------------------------------+
```

This gives S-CPU:

* **13 bits** for direct ROM addressing;
* **11 bits** for RAM, MMIO, Immediate, and Indirect modes.

In addressed modes, the encoded address selects a full **16-bit word**. The
width of the address field therefore does not limit the width of the data.

### Addressing Modes

The addressing mode determines how the operand field is interpreted and where
the operand value comes from.

| Bits 13–11 | Mode          | Payload                        | Meaning                                                                               |
| ---------- | ------------- | ------------------------------ | ------------------------------------------------------------------------------------- |
| `0xx`      | **ROM**       | 13-bit address                 | Access a 16-bit word from ROM address `0x0000–0x1FFF`; `xx` are part of the address     |
| `100`      | **RAM**       | 11-bit address                 | Access one of 2,048 16-bit RAM words                                                  |
| `101`      | **MMIO**      | 11-bit device/register address | Access one of eight MMIO devices                                                      |
| `110`      | **Immediate** | 11-bit value                   | Use the value stored directly in the instruction                                      |
| `111`      | **Indirect**  | 11-bit RAM address             | Resolve the operand through a pointer stored in RAM |

In **Immediate** mode, the payload is the operand value itself.

In **ROM**, **RAM**, and **MMIO** modes, the payload identifies where the CPU
obtains the 16-bit operand value.

**Indirect** mode adds one more level of resolution: the selected RAM word
contains another encoded operand that must first be resolved.

For `NOR` and `ADD`, the selected mode determines the source of the operand.

For `STA`, it determines the destination.

For `JCC`, the operand provides the destination used to update the Program
Counter when the Carry Flag is clear.

## Two-Phase Execution Cycle

S-CPU executes instructions using two alternating phases:

* **S0 — Fetch:** load the next instruction from ROM into the Instruction
  Register and increment the Program Counter.
* **S1 — Execute:** decode the instruction and execute one of the four native
  operations.

A normal direct instruction therefore requires **two clock ticks**:

```text
S0: fetch instruction
S1: execute instruction
```

The PC is incremented during S0, so while the current instruction executes in
S1, the PC already points to the following instruction.

### S0 — Fetch

During a normal fetch:

1. `ROM[PC]` is loaded into the **Instruction Register**.
2. `PC` is incremented so it points to the next program word.
3. If the previous instruction was `JCC`, the **Carry Flag** is cleared after
   its test.

There is one exception: when an indirect operand has just been resolved, the
**Indirected Flag** is set. In that case, S0 deliberately skips the ROM fetch
because the rewritten instruction already present in `IR` must execute first.

### S1 — Execute

During execution:

1. the CPU decodes the opcode;
2. it determines the addressing mode;
3. it resolves the operand;
4. it executes `NOR`, `ADD`, `STA`, or `JCC`.

Indirect addressing requires one additional resolution step.

### Indirect Addressing

Indirect addressing lets a program select an operand address dynamically.

Consider this example:

```asm
#bank userpage
value:   #res 1
pointer: #res 1

#bank prg
MOV pointer, #(value)
LDA @pointer
```

`#(value)` means **the address of `value`**. The assembler converts that address
to the native encoded operand form and stores it in `pointer`.

After the first instruction, `pointer` therefore contains the encoded address
of `value`, not the value stored in `value`.

The second instruction:

```asm
LDA @pointer
```

means: use the encoded operand stored in `pointer` as the operand of `LDA`.

In other words:

```text
pointer  → address of value
@pointer → value stored at that address
```

So `LDA @pointer` ultimately loads the 16-bit contents of `value` into the
accumulator.

Internally, `pointer` contains the **14-bit encoded operand field** that will
replace the operand currently stored in `IR`.

For example, if `value` is at RAM offset `0x100`, the assembler stores the
native RAM operand `0x2100` in `pointer`.

Execution then takes four clock phases:

| Phase | Action |
| ----- | ------ |
| **S0** | Fetch `LDA @pointer` into `IR` and increment `PC` |
| **S1** | Read `RAM[pointer]`, preserve the opcode, replace the operand bits in `IR`, and set the Indirected Flag |
| **S0** | Skip the normal fetch because the rewritten instruction is already in `IR` |
| **S1** | Clear the Indirected Flag and execute the rewritten instruction |

A direct instruction normally takes two clock ticks; an indirect instruction
normally takes four.

The additional skipped fetch is essential: otherwise the next program
instruction would overwrite the rewritten instruction before it could execute.

Indirect addressing is also essential to the S-CPU stack model. `SP` holds the
address of the current stack position, and instructions such as `PUSH` and
`POP` use indirection to access the RAM word pointed to by `SP`. `CALL` and
`RET` then build on `PUSH` and `POP` to implement subroutine calls and returns.

It also provides the foundation for **pointers in S-Code**. A pointer variable holds
an encoded RAM operand, so reading or writing through a pointer (`*ptr`) compiles
to an indirect access to the referenced word. The same mechanism enables pointer
arithmetic, array traversal, pass-by-address patterns, and dynamic memory allocation.

Without indirect addressing, an S-CPU program could only access addresses known
at assembly time.

## Extended Operands and Long Jumps

Native instructions cannot always encode a full 16-bit value directly.

Two limits are important:

* **Immediate mode** can encode only an 11-bit value (`0x0000–0x07FF`).
* **Direct ROM mode** can address only the first 8,192 ROM words
  (`0x0000–0x1FFF`), although each selected ROM word still contains a full
  16-bit value.

When a value does not fit in the 11-bit immediate field, the assembler can place
the full 16-bit value in a **low-ROM constant pool** and rewrite the instruction
to read it from there.

The same principle is used for both extended immediate values and long jumps.

### Extended Immediate Values

For example:

```asm
LDA #0x1234
```

`0x1234` does not fit into the 11-bit immediate field.

The assembler can therefore transform it conceptually into:

```asm
LDA const_0

...

const_0:
#d16 0x1234
```

`const_0` is placed below ROM address `0x2000`, so it can be addressed using the
13-bit direct ROM mode.

The instruction does not contain `0x1234` itself. It contains the ROM address of
a word that contains `0x1234`.

### Long Jumps

`JCC` loads a destination address into the Program Counter when the Carry Flag is clear.

If the destination fits in 11 bits, its address is encoded directly in the instruction
using immediate mode.

If the destination is larger than `0x07FF`, it cannot fit in the immediate
field. The assembler then stores the full 16-bit destination in the low-ROM
constant pool and makes `JCC` read that value from ROM:

```asm
; Source
JCC farRoutine

; Conceptual result
JCC const_0

...

const_0:
#d16 farRoutine
```

`const_0` is placed below `0x2000`, so its address fits in the 13-bit direct ROM
operand field. The word stored at `const_0`, however, is a full 16-bit value and
can therefore contain any ROM destination from `0x0000` to `0xFFFF`.

The assembler handles this transformation automatically, so source code can use
normal labels without worrying about the 11-bit immediate limit.

The constant pool is inserted between the bootloader and `ENTRY_POINT` and must
remain below `0x2000`. Identical constants can be reused.

A normal data access to a ROM address above `0x1FFF` still cannot be encoded as
a single native direct-ROM operand.

See
[Extended Operands & Long Jump](../software/assembler/README.md#extended-operands--long-jump-assembler-details)
for the assembler-specific rewriting and layout rules.

## Memory Model

### Separate Native Spaces

S-CPU has three separate native address spaces:

* **ROM** for program code and constants;
* **RAM** for variables, stack, and working storage;
* **MMIO** for external devices.

They are selected by the addressing bits of the instruction and are not simply
three regions of one physical address bus.

| Space | Capacity | Native address | Instruction operand |
| ----- | -------- | -------------- | ------------------- |
| **ROM** | 65,536 words (128 KiB) | `0x0000–0xFFFF` | Direct operands can address `0x0000–0x1FFF` |
| **RAM** | 2,048 words (4 KiB) | `0x000–0x7FF` | `100` + 11-bit address |
| **MMIO** | 8 × 256 registers | device 0–7, register `0x00–0xFF` | `101` + device/register address |

The Program Counter is independent of operand addressing and covers the entire
16-bit ROM space.

Sequential execution can therefore continue beyond `0x1FFF`, and long jumps
can target any ROM address from `0x0000` to `0xFFFF`.

The 13-bit ROM limit applies only when a native instruction uses a ROM word as
its **operand**.

## Assembler Virtual Address Space

Native operand values would be ambiguous if they were used directly in source
code.

For example, encoded operand `0x2000` means **RAM offset 0**, but `0x2000` is
also a valid address in the 64K-word ROM.

The assembler therefore exposes a **17-bit virtual address space** that keeps
ROM, RAM, and MMIO unambiguous:

| Assembler address | Meaning | Native encoding |
| ----------------- | ------- | --------------- |
| `0x00000–0x0FFFF` | ROM | ROM address |
| `0x12000–0x127FF` | RAM | subtract `0x10000` → `0x2000–0x27FF` |
| `0x12800–0x12FFF` | MMIO | subtract `0x10000` → `0x2800–0x2FFF` |

For example:

* `0x02000` means ROM address `0x2000`;
* `0x12000` means RAM offset `0x000`;
* `0x127FF` means RAM offset `0x7FF`;
* `0x12800` means MMIO device #0, register `0x00`.

These 17-bit addresses exist only in source code, symbols, and development
tools.

The CPU never manipulates a 17-bit address. The assembler converts it into the
native representation required by the instruction.

## RAM Layout

The assembler divides the 2K-word RAM into three areas:

| Virtual range | Size | Purpose |
| ------------- | ---- | ------- |
| `0x12000–0x120FF` | 256 words | System stack used by `PUSH`, `POP`, `CALL`, and `RET` |
| `0x12100–0x126FF` | 1,536 words | User page for variables, pointers, and buffers |
| `0x12700–0x127FF` | 256 words | Reserved registers and compiler/assembler working storage |

`R0` through `R9` are general-purpose registers located in the reserved register
area.

Other predefined registers such as `SP`, `FP`, `RPAR`, `RPEEK`, and `RRET` are
used internally by the architecture, compiler, or assembler macros. Programs
should therefore not rely on them to retain arbitrary data across macro calls.

## Memory-Mapped I/O

MMIO uses the 11-bit payload as:

```text
10          8 7                                      0
+-------------+---------------------------------------+
| device (3)  | register (8)                          |
+-------------+---------------------------------------+
```

This provides:

* **8 devices**;
* **256 registers per device**.

The corresponding assembler virtual address is:

```text
0x12800 + (DeviceID << 8) + Register
```

For example, device #0 exposes the standard demonstration outputs:

* `0x12801` — 16-bit hexadecimal display
* `0x12802` — LED indicator or LED bank, depending on the implementation

```asm
; Turn on the status LED
MOV 0x12802, #1

; Display a value
MOV 0x12801, #0x1234
```

## Reserved Device #7

MMIO device #7 (`0x12F00–0x12FFF`) is reserved for S-CPU internal functions.

| Register | Virtual address | Function |
| -------- | --------------- | -------- |
| `CF` (`0x0F`) | `0x12F0F` | Any write sets the Carry Flag |
| `RSINK` (`0x10`) | `0x12F10` | Write-only sink; written values are discarded and reads are undefined |

These registers are used by simple assembler macros.

Writing to `CF` sets the Carry Flag without modifying the accumulator:

```asm
[macro sec]
STA CF
```

Writing to `RSINK` discards the accumulator value while preserving both the
accumulator and Carry Flag:

```asm
[macro nop]
STA RSINK
```

This provides the architectural `SEC` and `NOP` operations using a single native
`STA` instruction.

`RSINK` must not be read because no read value is guaranteed.
