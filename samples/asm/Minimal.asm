; =============================================================================
; S-CPU SAMPLE - MINIMAL PROGRAM
; =============================================================================
; What it does:
;   Builds the value 42 using only the four native CPU instructions.
; Expected result:
;   The device-0 hexadecimal display shows 42 (0x002A).
; Runs on:
;   Desktop/CLI simulators and targets exposing the device-0 display.
; Demonstrates:
;   NOR, ADD, STA, and JCC without any macro instruction.
; =============================================================================

#bank prg

start:
  NOR #0xFFFF      ; clear A: ~(A | 0xFFFF) = 0, the native form of CLR
  ADD #42          ; A = 42
  ; IODEV starts the MMIO region; offset 1 is device 0's hexadecimal display.
  STA IODEV+1

stop:
  ; HALT expands to two consecutive self-looping JCC instructions. If carry is
  ; clear, the first one loops; if carry was set, JCC clears it and the second
  ; one loops. Writing both instructions explicitly keeps this sample macro-free.
  JCC stop

stopAfterCarry:
  JCC stopAfterCarry
