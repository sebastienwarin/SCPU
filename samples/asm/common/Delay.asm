; ------ ASSEMBLER CONSTANTS FOR DELAY ------
#const FREQ_HZ         = 2_000_000       ; SCPU Frequency in Hz
#const CYCLES_PER_MS   = FREQ_HZ / 1000  ; CPU cycles in one millisecond
#const CYCLES_PER_LOOP = 14              ; Cycles for 1 loop (DEC + STA + JNZ)
#const ITERATIONS_PER_MS = CYCLES_PER_MS / CYCLES_PER_LOOP
; ---------------------------------------------

; function delay(uint ms)
delay:
  lds #2, R1 ; R1 = ms
  .wait_ms:
    ; Wait one millisecond
    mov R0, #ITERATIONS_PER_MS
    .loop:    ; 7 instructions = 14 cycles
      dec R0
      sta R0
      jnz .loop
    ; Loop until delay not reached
    dec R1
    sta R1
    jnz .wait_ms
    ; Else return
    ret
