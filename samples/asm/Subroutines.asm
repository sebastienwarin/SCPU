; =============================================================================
; S-CPU SAMPLE - SUBROUTINES
; =============================================================================
; What it does:
;   Calls a subroutine that calls a second subroutine and updates two variables.
; Expected result:
;   After HALT, `sourceValue` is 10 and `copiedValue` is 15 in the debugger RAM view.
; Runs on:
;   Desktop/CLI simulators and all S-CPU hardware implementations.
; Demonstrates:
;   Nested CALL/RET sequences and shared RAM variables.
; =============================================================================

#bank userpage

; Each #res 1 reserves one 16-bit RAM word for one variable.
sourceValue: #res 1
copiedValue: #res 1

#bank prg

start:
  mov sourceValue, #15
  ; CALL saves a return address before jumping to `copyThenUpdate`.
  call copyThenUpdate
  halt

copyThenUpdate:
  mov copiedValue, sourceValue
  ; Nested calls use the same stack. Each RET resumes after its matching CALL.
  call updateSourceValue
  ret

updateSourceValue:
  mov sourceValue, #10
  ret
