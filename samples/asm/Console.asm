; =============================================================================
; S-CPU SAMPLE - CONSOLE
; =============================================================================
; What it does:
;   Displays a command prompt, captures a line, then prints the completed line.
; Expected result:
;   The terminal displays "scpu> "; typed characters are echoed immediately and
;   pressing Enter prints the captured line before displaying a new prompt.
;   Input is safely limited to 19 characters per line.
; Runs on:
;   Desktop/CLI, Digital and Logisim simulators, and targets exposing the
;   device-1 TTY/keyboard.
; Demonstrates:
;   Bounded input, null-terminated strings, pointers, subroutines, and TTY MMIO.
; =============================================================================

; IODEV is the base address of the MMIO region. Adding 0x100 selects device 1,
; the TTY/keyboard device implemented by the Desktop, CLI, Digital and Logisim
; simulators. The final offset selects one of that device's MMIO registers.
#const TTY_OUTPUT = IODEV+0x101          ; device 1, register 1: character output
#const TTY_INPUT = IODEV+0x102           ; device 1, register 2: buffered input
#const TTY_INPUT_AVAILABLE = IODEV+0x103 ; device 1, register 3: input status
#const LF_CODE = 10
#const INPUT_CAPACITY = 20

#bank userpage

; The buffer lives in RAM. #res counts 16-bit words, so this reserves 20 words;
; one of them is kept free for the final zero terminator.
input: #res INPUT_CAPACITY
inputLength: #res 1            ; one 16-bit RAM word

#bank prg

start:
  ; `print` receives its string address through the stack.
  push #(title)
  call print
  pop

prompt:
  ; Print a fresh prompt at the beginning of every input line.
  push #(promptText)
  call print
  pop

waitForCharacter:
  ; Poll first: reading TTY_INPUT when no character is available is not useful.
  lda TTY_INPUT_AVAILABLE
  jz waitForCharacter

  ; R2 points to input[inputLength], the next free word in the RAM buffer.
  lda #(input)
  add inputLength
  sta R2

  ; Keep a copy in R1 because the following comparisons change the accumulator.
  ; Writing the same character to TTY_OUTPUT provides immediate local echo.
  lda TTY_INPUT
  sta R1
  sta TTY_OUTPUT
  ; Enter is encoded as LF (10). It submits the line instead of entering it in
  ; the buffer.
  sub #LF_CODE
  jz submitLine

  ; Stop storing at 19 characters, but continue accepting Enter. This preserves
  ; the final word for '\0' and prevents writes beyond the reserved RAM buffer.
  lda inputLength
  sub #(INPUT_CAPACITY-1)
  jz waitForCharacter

  ; Store a normal character through the pointer and advance the logical length.
  lda R1
  sta @(R2)
  inc inputLength
  sta inputLength
  jmp waitForCharacter

submitLine:
  ; Convert the captured bytes to a null-terminated string for `print`.
  lda #0
  sta @(R2)
  ; Show the captured line, move to the next terminal line, then reset the buffer.
  push #(input)
  call print
  pop
  push #(newLine)
  call print
  pop
  mov inputLength, #0
  jmp prompt

title: #d "S-CPU console", "\n", 0
promptText: #d "scpu> ", 0
newLine: #d "\n", 0

print:
  ; CALL already pushed its return address, so the caller's first argument is
  ; two stack slots above SP. LDS loads that string pointer into R0.
  lds #2, R0
printNext:
  mov TTY_OUTPUT, @(R0) ; dereference R0 and write one character to MMIO
  jz printDone         ; '\0' terminates the string
  inc R0
  sta R0
  jmp printNext
printDone:
  ret
