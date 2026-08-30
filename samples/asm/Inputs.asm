; =============================================================================
; S-CPU SAMPLE - DIGITAL INPUTS
; =============================================================================
; What it does:
;   Reads the device-3 input board in Digital and shows the button state on the
;   LEDs. Switch 7 acts as a display toggle: when it is on, the button nibble is
;   inverted before being shown.
; Expected result:
;   Buttons 0-3 are shown on the lower nibble. When switch 7 is on, the button
;   nibble is inverted.
; Runs on:
;   Digital, or the physical S-CPU TTL with the device-3 input board.
; Real hardware required:
;   No for Digital.
; Demonstrates:
;   MMIO input, bit masks, conditional control flow, and MMIO output.
; =============================================================================

; IODEV is the MMIO base. Device 0 register 2 drives the LEDs; adding 0x300
; selects physical device 3, whose register 1 exposes the input board.
#const LED_BANK = IODEV+2
#const INPUT_BOARD = IODEV+0x301

#bank prg
start:

  ; Read the Digital input board.
  mov R0, INPUT_BOARD

  ; If switch 7 is enabled, invert the full byte before displaying it.
  and #0x80
  jz .showRaw

  lda R0
  not
  sta R0

.showRaw:
  mov LED_BANK, R0

  jmp start
