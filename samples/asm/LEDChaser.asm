; =============================================================================
; S-CPU SAMPLE - LED CHASER (K2000 EFFECT)
; =============================================================================
; What it does:
;   Recreates the K2000 scanner effect on the device-0 LED bank.
; Expected result:
;   The lit LED moves from one side to the other and back every 100 ms.
; Runs on:
;   Desktop/CLI simulators and targets exposing the device-0 LED bank. Set
;   FREQ_HZ to the actual target clock for accurate timing.
; Demonstrates:
;   ROM lookup tables, indirect addressing, MMIO, and timed loops.
; =============================================================================

#include "common/Delay.asm"

#const LED_BANK = IODEV+2

#bank userpage
ledIndex: #res 1  ; one 16-bit RAM word stores the current pattern index

#bank prg

start:
  MOV ledIndex, #0

  loop:
    ; R2 = &ledPattern[ledIndex] 
    lda #ledPattern
    add ledIndex
    sta R2
    ; IODEV is the MMIO base; device 0 register 2 is the LED output register.
    ; @R2 reads the current pattern word indirectly from ROM.
    mov LED_BANK, @R2

    ; Wait 100 ms
    push #100
    call delay
    pop

    ; Increment index & loop
    inc ledIndex
    sta ledIndex
    sub ledPatternLength
    jz .resetLoop
    jmp loop
    .resetLoop:
      mov ledIndex, #0
      jmp loop

ledPattern: #d16 8, 4, 2, 1, 2, 4
ledPatternLength: #d16 ledPatternLength - ledPattern
