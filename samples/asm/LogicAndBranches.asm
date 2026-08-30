; =============================================================================
; S-CPU SAMPLE - LOGIC AND BRANCHES
; =============================================================================
; What it does:
;   Runs three small checks using bit masks, zero tests, and conditional jumps.
; Expected result:
;   The hexadecimal display shows 3 and the device-0 LED turns on. A smaller
;   value means execution stopped at the corresponding failed check.
; Runs on:
;   Desktop/CLI simulators and targets exposing the device-0 display and LED.
; Demonstrates:
;   AND, SUB, JZ, JNZ, labels, and a simple success/failure pattern.
; =============================================================================

; IODEV starts the MMIO region. Offsets 1 and 2 select the hexadecimal display
; and LED registers of device 0.
#const HEX_DISPLAY = IODEV+1
#const LED_BANK = IODEV+2

#bank prg

start:
  ; R0 counts successful checks. If one fails, its value identifies the check.
  mov R0, #0

  ; Check 1: mask every bit except the high bit. A zero result means "not set".
  lda #0xC9
  and #0x80
  jz failed
  inc R0
  sta R0

  ; Check 2: bit 0 is zero for every even number.
  lda #42
  and #1
  jnz failed
  inc R0
  sta R0

  ; Check 3: subtracting two equal values produces zero.
  lda #12
  sub #12
  jnz failed
  inc R0
  sta R0

  ; All checks passed: publish both a numeric count and a visible success LED.
  mov HEX_DISPLAY, R0
  mov LED_BANK, #1
  halt

failed:
  ; R0 contains the number of checks completed before the failure.
  mov HEX_DISPLAY, R0
  mov LED_BANK, #0
  halt
