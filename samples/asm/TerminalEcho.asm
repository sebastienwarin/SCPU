; =============================================================================
; S-CPU SAMPLE - TERMINAL ECHO
; =============================================================================
; What it does:
;   Copies every keyboard character directly to the terminal output.
; Expected result:
;   Each typed character appears immediately in the terminal.
; Runs on:
;   Desktop/CLI, Digital and Logisim simulators, and targets exposing the
;   device-1 TTY/keyboard.
; Demonstrates:
;   Keyboard availability polling, character input, terminal MMIO, and a loop.
; =============================================================================

; IODEV is the base address of the MMIO region. Adding 0x100 selects device 1,
; the TTY/keyboard device implemented by the Desktop, CLI, Digital and Logisim
; simulators. The final offset selects one of that device's MMIO registers.
#const TTY_OUTPUT = IODEV+0x101          ; device 1, register 1: character output
#const TTY_INPUT = IODEV+0x102           ; device 1, register 2: buffered input
#const TTY_INPUT_AVAILABLE = IODEV+0x103 ; device 1, register 3: input status
#bank prg

start:
waitForCharacter:
  ; Poll the status register so TTY_INPUT is read only when input exists.
  lda TTY_INPUT_AVAILABLE
  jz waitForCharacter

  ; Reading TTY_INPUT consumes one character; TTY_OUTPUT displays it immediately.
  lda TTY_INPUT
  sta TTY_OUTPUT
  jmp waitForCharacter
