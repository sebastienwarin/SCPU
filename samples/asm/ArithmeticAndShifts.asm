; =============================================================================
; S-CPU SAMPLE - ARITHMETIC AND SHIFTS
; =============================================================================
; What it does:
;   Multiplies 6 by 7 using repeated addition, shifts the result left once,
;   then shifts it right to recover the original value.
; Expected result:
;   The hexadecimal display shows 42 (0x002A).
; Runs on:
;   Desktop/CLI simulators and targets exposing the device-0 display.
; Demonstrates:
;   ADD, DEC, loop construction, LSL, and LSR.
; =============================================================================

; IODEV is the MMIO base; offset 1 selects device 0's hexadecimal display.
#const HEX_DISPLAY = IODEV+1

#bank prg

start:
  mov R0, #6          ; value to add
  mov R1, #7          ; number of additions
  mov R2, #0          ; result

multiplyLoop:
  ; R2 += R0 once per iteration. R1 is the remaining iteration count.
  lda R2
  add R0
  sta R2
  dec R1
  sta R1
  jnz multiplyLoop    ; DEC reaches zero after seven additions

  ; Logical shifts move every bit and fill the newly opened bit with zero.
  lda R2              ; 42
  lsl                 ; 84: one-bit left shift doubles the value
  lsr                 ; 42: one-bit right shift divides it by two
  sta HEX_DISPLAY
  halt
