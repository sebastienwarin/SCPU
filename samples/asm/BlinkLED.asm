; =============================================================================
; S-CPU SAMPLE - BLINK LED
; =============================================================================
; What it does:
;   Toggles the device-0 LED every 500 milliseconds.
; Expected result:
;   The LED repeatedly turns on and off at a steady one-second cycle.
; Runs on:
;   Desktop/CLI simulators and targets exposing the device-0 LED. Set FREQ_HZ
;   to the actual target clock for accurate timing.
; Demonstrates:
;   MMIO output, state, an infinite loop, includes, and reusable delay code.
; =============================================================================

#include "common/Delay.asm"

#const LED_BANK = IODEV+2

#bank userpage

ledState: #res 1  ; #res 1 reserves one 16-bit word in RAM

#bank prg

start: 
  ; Initial state (LED is off)
  mov ledState, #0

  loop:
    ; IODEV is the start of the MMIO region. Offset 2 addresses register 2 of
    ; device 0, which is the LED output register in the simulators and hardware.
    mov LED_BANK, ledState
    
    ; Wait 1/2 second = delay(500)
    push #500
    call delay
    pop

    ; Toggle LED and loop forever
    lda ledState
    jz .turnOn
    .turnOff:
      mov ledState, #0
      jmp loop
    .turnOn:
      mov ledState, #1
      jmp loop
