# S-CPU Legacy Assembler (customasm rules)

⚠️ **LEGACY / NOT MAINTAINED ANYMORE**  
This folder contains the historical implementation of the S-CPU assembler using [customasm](https://github.com/hlorenzi/customasm).  
It is preserved **for reference only**. The active assembler is now implemented in C# under [`software/assembler`](../../software/assembler).  

## Instruction format

All instructions are 16-bit words with the following bit pattern:

`OO XXX AAA AAAA AAAA` 

* `OO` : Opcode [15..14]
* `XXX` : Address mode [13..11]
* `AAA AAAA AAAA` : Operand (address or immediate value) [10..0]

## Instruction set

Each instruction is encoded using a 2-bit opcode, followed by an 11-bit operand interpreted according to the address mode.

* `00` : `NOR {value_or_address}` Performs a bitwise NOR between the accumulator and the specified immediate value or memory contents.
* `01` : `ADD {value_or_address}` Adds the specified immediate value or the contents of the specified memory address to the accumulator.
* `10` : `STA {address}` Stores the contents of the accumulator into the specified memory address.
* `11` : `JCC {address}` Jumps to the specified address if the carry flag is not set (C=0).

## Address mode

The CPU uses 3-bit addressing modes to interpret the 11-bit operand in different ways, enabling access to multiple memory regions and addressing types.

* `0AA` : ROM (13-bit address width - 8K address space)
* `100` : RAM (11-bit address width - 2K address space)
* `101` : I/O devices (Memory mapped I/O - 11-bit address width - 2K address space)
* `110` : Immediate value (11-bit encoded value)
* `111` : Indirect mode (11-bit RAM address)

## Macros

These are pseudo-instructions that are expanded by the assembler into one or more low-level instructions.

* `NOP` Performs no operation. Internally implemented as ADD #0, leaving the accumulator unchanged but consuming one instruction (2 cycles).
* `CLR` Sets the accumulator to 0.
* `LDA {value_or_address}` Loads the specified immediate value or the contents of the specified memory address into the accumulator.
* `MOV {address}, {value_or_address}` Moves an immediate value or the contents of the specified memory address into another memory location.
* `JCS {address}` Jumps to the specified address if the carry flag is set (C=1).
* `JZ {address}` Jumps to the specified address if the accumulator is zero (A=0).
* `JNZ {address}` Jumps to the specified address if the accumulator is not zero (A≠0).
* `JMP {address}` Unconditionally jumps to the specified address.
* `NOT` Inverts the bits of the accumulator (bitwise NOT operation).
* `NOT {value_or_address}` Inverts the bits of the specified immediate value or the contents of the specified memory (bitwise NOT operation).
* `AND {value_or_address}` Performs a bitwise AND between the accumulator and the specified immediate value or memory contents.
* `NAND {value_or_address}` Performs a bitwise NAND between the accumulator and the specified immediate value or memory contents.
* `OR {value_or_address}` Performs a bitwise OR between the accumulator and the specified immediate value or memory contents.
* `XOR {value_or_address}` Performs a bitwise XOR between the accumulator and the specified immediate value or memory contents.
* `LSL` Performs a logical shift left (LSL) on the accumulator, shifting all bits one position to the left.
* `LSL {value_or_address}` Performs a logical shift left (LSL) on the specified immediate value or the contents of the specified memory address.
* `LSR` Performs a logical shift right (LSR) on the accumulator, shifting all bits one position to the right.
* `LSR {value_or_address}` Performs a logical shift right (LSR) on the specified immediate value or the contents of the specified memory address.
* `ROL` Rotates the bits of the accumulator one position to the left.
* `ROL {value_or_address}` Rotates the bits of the specified immediate value or the contents of the specified memory address one position to the left.
* `ROR` Rotates the bits of the accumulator one position to the right.
* `ROR {value_or_address}` Rotates the bits of the specified immediate value or the contents of the specified memory address one position to the right.
* `INC` Increments the value of the accumulator by 1.
* `INC {value_or_address}` Increments the specified immediate value or the contents of the specified memory address by 1.
* `DEC` Decrements the value of the accumulator by 1.
* `DEC {value_or_address}` Decrements the specified immediate value or the contents of the specified memory address by 1.
* `NEG` : Negates the value of the accumulator (two's complement).
* `NEG {value_or_address}` : Negates the specified immediate value or the contents of the specified memory address (two's complement).
* `SUB {value_or_address}` Subtracts the specified immediate value or the contents of the specified memory address from the accumulator.
* `ADC {value_or_address}` Adds the specified immediate value or the contents of the specified memory address to the accumulator, including the value of the Carry flag.
* `SBC {value_or_address}` Subtracts the specified immediate value or the contents of the specified memory address from the accumulator, including the value of the Carry flag.
* `LDC {value_or_address}` Loads the specified immediate value or the contents of the specified memory address into the accumulator. If the Carry flag was set before this operation, it is preserved and maintained after loading the value into the accumulator.
* `CLC` Clear the Carry flag to 0.
* `SEC` Sets the Carry flag to 1.
* `PUSH` Pushes the contents of the accumulator onto the stack.
* `PUSH {value_or_address}` Pushes an immediate value or the contents of the specified memory address onto the stack.
* `POP` Pops the top value from the stack and stores it in the accumulator.
* `POP {address}` Pops the top value from the stack and stores it in the specified memory address.
* `LDS {index}` Loads the value at the specified stack index and places it in the accumulator.
* `LDS {index}, {address}` Loads the value at the specified stack index and places it in the specified memory address.
* `STS {index}` Stores the contents of the accumulator into the specified stack index.
* `STS {index}, {value_or_address}` Stores the contents of the immediate value or the contents of the specified memory address into the specified stack index.
* `CALL {address}` Saves the current program counter (PC) on the stack and jumps to the specified address.
* `RET` Returns to the address saved on the stack during the last CALL.
* `RST` Resets the program counter (PC) to zero.
* `HALT` Enters an infinite loop, effectively halting the program.

## Memory map

The S-CPU features a unified 16-bit address space split into ROM, RAM, and memory-mapped I/O regions. Each region serves a distinct purpose and is organized into well-defined sub-sections for efficient code, data, and peripheral access.

 * 0x0000 -> 0x1FFF : ROM (8K - size: 0x2000)
    * Header: 0x00 -> 0x7F (size: 0x80)
    * Global consts: 0x80 -> 0xFF (size: 0x80)
    * User program: 0x100 -> 0x1FFF (size: 0x1F00)
 * 0x2000 -> 0x27FF : RAM (2K - size: 0x800)
    * Zero page: 0x2000 -> 0x20FF (size: 0x100)
    * User page: 0x2100 -> 0x26FF (size: 0x600)
    * Reserved page : 0x2700 -> 0x27FF (size: 0x100)
        * Registers: 0x2700
            * R0 : 0x2700
            * R1 : 0x2701
            * R2 : 0x2702
            * R3 : 0x2703
            * R4 : 0x2704  
            * R5 : 0x2705
            * R6 : 0x2706
            * R7 : 0x2707
            * R8 : 0x2708
            * R9 : 0x2709
        * Parameter register (RPAR) : 0x270A
        * Frame pointer (FP) : 0x270E
        * Stack pointer (SP) : 0x270F
        * Temp Variables (TEMPVAR) : 0x2710 -> 0x27FF (size: 0xF0)
 * 0x2800 -> 0x2FFF : I/O devices (2K - size: 0x800)
   * Device #0: 0x2800 -> 0x28FF
   * Device #1: 0x2900 -> 0x29FF
   * Device #2: 0x2A00 -> 0x2AFF
   * Device #3: 0x2B00 -> 0x2BFF
   * Device #4: 0x2C00 -> 0x2CFF
   * Device #5: 0x2D00 -> 0x2DFF
   * Device #6: 0x2E00 -> 0x2EFF
   * Device #7: 0x2F00 -> 0x2FFF - Reserved for SCPU

## Usage (historical)

To assemble code with `customasm`:

```sh
customasm.exe hello.asm -f logisim16 -o rom.bin -- -f symbols -o symbol.txt -- -f annotated,base:16,group:4 -o rom_annotated.txt```
```

📌 Note: This assembler ruleset is no longer maintained.