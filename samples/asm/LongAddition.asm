; =============================================================================
; S-CPU SAMPLE - 32-BIT ADDITION
; =============================================================================
; What it does:
;   Adds two 32-bit unsigned values represented as pairs of 16-bit words, the
;   same storage width used by the S-Code `long` type.
; Expected result:
;   `sumHigh` is 0x0003 and `sumLow` is 0x25AB, so the complete result is
;   0x0003_25AB (206251). The hexadecimal display shows the low word 0x25AB.
; Runs on:
;   Desktop/CLI simulators and targets exposing the device-0 hex display.
; Demonstrates:
;   The representation of `long`, #d32 layout, low-to-high addition, LDC, ADC,
;   and carry propagation between words.
; =============================================================================

; IODEV is the MMIO base; offset 1 selects device 0's hexadecimal display.
#const HEX_DISPLAY = IODEV+1

#bank userpage
; A 32-bit result needs two consecutive 16-bit RAM words.
sumHigh: #res 1
sumLow:  #res 1

#bank prg

; #d32 stores the low word first and the high word second.
numberA: #d32 122000  ; 0x0001_DC90
numberB: #d32 84251   ; 0x0001_491B

; Because ROM data precedes the code, mark the first executable instruction.
ENTRY_POINT:
  ; Add the low words first. 0xDC90 + 0x491B produces 0x25AB and carry = 1.
  lda numberA
  add numberB
  sta sumLow

  ; LDC loads without losing the carry from the low-word addition. ADC then
  ; includes that carry in the high-word addition: 1 + 1 + 1 = 3.
  ldc numberA+1
  adc numberB+1
  sta sumHigh

  ; Show the low word on the standard device-0 hexadecimal display.
  mov HEX_DISPLAY, sumLow
  halt
