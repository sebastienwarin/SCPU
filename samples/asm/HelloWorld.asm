; =============================================================================
; S-CPU SAMPLE - HELLO WORLD
; =============================================================================
; What it does:
;   Writes the universal "Hello, World!" greeting to the terminal.
; Expected result:
;   The terminal displays "Hello, World!" followed by a new line, then halts.
; Runs on:
;   Desktop/CLI, Digital and Logisim simulators, and targets exposing the
;   device-1 TTY.
; Demonstrates:
;   A ROM string, pointer-based iteration, terminal MMIO, and program completion.
; =============================================================================

; IODEV starts the MMIO region; +0x100 selects device 1 and +1 its TTY output
; register. This device is implemented by the software, Digital and Logisim
; simulators.
#const TTY_OUTPUT = IODEV+0x101

#bank prg

; #d stores words in ROM. The final zero terminates the string.
message: #d "Hello, World!", "\n", 0

; The data appears before the code, so define the program entry explicitly.
ENTRY_POINT:
  mov R0, #(message)     ; R0 points to the first character in ROM
  jmp printCharacter

advancePointer:
  inc R0                 ; advance to the next ROM address
  sta R0

printCharacter:
  mov TTY_OUTPUT, @(R0)  ; @ dereferences R0 and writes that character to MMIO
  jnz advancePointer     ; zero marks the end of the string
  halt
