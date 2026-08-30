; =============================================================================
; S-CPU SAMPLE - STACK
; =============================================================================
; What it does:
;   Pushes two variables and pops them in reverse order to swap their values.
; Expected result:
;   After HALT, `firstValue` is 10 and `secondValue` is 15 in the debugger RAM view.
; Runs on:
;   Desktop/CLI simulators and all S-CPU hardware implementations.
; Demonstrates:
;   PUSH, POP, LIFO ordering, and RAM variables.
; =============================================================================

#bank userpage

; Each #res 1 reserves one 16-bit RAM word for one variable.
firstValue: #res 1
secondValue: #res 1

#bank prg

start:
  mov firstValue, #15
  mov secondValue, #10

  ; PUSH grows the stack and stores each value. POP reads in reverse order
  ; (last in, first out), which swaps the variables without a temporary word.
  push firstValue
  push secondValue
  pop firstValue
  pop secondValue
  halt
