; =============================================================================
; S-CPU SELF-TEST
; =============================================================================
; What it does:
;   Validates a complete S-CPU implementation by exercising native instructions,
;   addressing modes, macros, branches, shifts, stack operations, and carry.
; Expected result:
;   Every conforming S-CPU implementation must pass this test: the device-0 LED
;   turns on. On failure, the hexadecimal display shows the error code below.
; Runs on:
;   Desktop/CLI simulators and all S-CPU hardware implementations.
; Demonstrates:
;   The common conformance and regression test for every S-CPU implementation.
; =============================================================================

; IODEV is the MMIO base. Device 0 exposes the observable test result through:
; IODEV+1 is the hexadecimal display and IODEV+2 is the LED bank.
#const LED_BANK    = IODEV+2
#const HEX_DISPLAY = IODEV+1

; ---------- error codes ----------
#const ERR_ADD_IMM    = 1
#const ERR_ADD_MEM    = 2
#const ERR_ADD_IND    = 3
#const ERR_NOR_IMM    = 4
#const ERR_NOR_MEM    = 5
#const ERR_NOR_IND    = 6
#const ERR_STA        = 7
#const ERR_JCC        = 8
#const ERR_LOGIC      = 9
#const ERR_MATH       = 10
#const ERR_STACK      = 11
#const ERR_BRANCH     = 12
#const ERR_SHIFT      = 13
#const ERR_STACKIDX   = 15
#const ERR_CARRY      = 16
#const ERR_CARRY_UPD  = 17
#const ERR_STATE      = 18
#const ERR_LOGIC_CF   = 19
#const ERR_MATH_CF    = 20
#const ERR_ADC_VALUE  = 21
#const ERR_ADC_CF     = 22
#const ERR_SBC_VALUE  = 23
#const ERR_SBC_CF     = 24
#const ERR_MEMORY     = 25

;===============================================================
#bank userpage
; Each #res 1 reserves one 16-bit RAM word used as self-test working storage.
testValueA:   #res 1
testValueB:   #res 1
testPointer: #res 1
testActual:  #res 1
testGuard:   #res 1

;===============================================================
#bank prg
start:
    CALL core_add_tests
    CALL core_nor_tests
    CALL core_sta_test
    CALL core_jcc_test
    CALL core_carry_test

    CALL macro_state_test
    CALL macro_logic_test
    CALL macro_math_test
    CALL macro_stack_test
    CALL macro_branch_test
    CALL macro_adc_test
    CALL macro_shift_test
    CALL macro_stack_idx_test

success:
    MOV LED_BANK, #1
halt_ok:
    HALT

;===============================================================
;  BLOCK A - core opcode tests
;===============================================================
core_add_tests:
    CLR
    ADD #5
    SUB #5
    JNZ fail_add_imm

    LDA #10
    STA testValueA
    CLR
    ADD testValueA
    SUB #10
    JNZ fail_add_mem

    LDA #(testValueA)
    STA testPointer     ; MOV testPointer, #(testValueA)
    CLR
    ADD @testPointer
    SUB #10
    JNZ fail_add_ind
    RET

fail_add_imm:
    MOV HEX_DISPLAY, #ERR_ADD_IMM
    HALT
fail_add_mem:
    MOV HEX_DISPLAY, #ERR_ADD_MEM
    HALT
fail_add_ind:
    MOV HEX_DISPLAY, #ERR_ADD_IND
    HALT

;---------------------------------------------------------------
core_nor_tests:
    CLR
    NOR #0
    ADD #1
    JCC fail_nor_imm

    LDA #15
    STA testValueB
    CLR
    NOR testValueB
    ADD testValueB
    ADD #1
    JCC fail_nor_mem

    MOV testPointer, #(testValueB)
    CLR
    NOR @testPointer
    ADD testValueB
    ADD #1
    JCC fail_nor_ind
    RET

fail_nor_imm:
    MOV HEX_DISPLAY, #ERR_NOR_IMM
    HALT
fail_nor_mem:
    MOV HEX_DISPLAY, #ERR_NOR_MEM
    HALT
fail_nor_ind:
    MOV HEX_DISPLAY, #ERR_NOR_IND
    HALT

;---------------------------------------------------------------
core_sta_test:
    LDA #0x0123
    STA testValueA
    CLR
    ADD testValueA
    SUB #0x0123
    JNZ fail_sta
    RET

fail_sta:
    MOV HEX_DISPLAY, #ERR_STA
    HALT

;---------------------------------------------------------------
core_jcc_test:
    CLC
    JCC jcc_clear_ok

fail_jcc:
    MOV HEX_DISPLAY, #ERR_JCC
    HALT

jcc_clear_ok:
    CLR
    NOR #0
    ADD #1
    JCC fail_jcc
    RET

;===============================================================
;  BLOCK B - macro tests
;===============================================================
macro_state_test:
    MOV testGuard, #0x5A5A

    ; NOP is STA RSINK: only write it, never read it. Check A and both C states.
    LDA #0x0000
    CLC
    NOP
    JCC .nop_0_ok
    JMP fail_state
.nop_0_ok:
    SUB #0x0000
    JNZ fail_state

    LDA #0x0001
    SEC
    NOP
    JCS .nop_1_ok
    JMP fail_state
.nop_1_ok:
    SUB #0x0001
    JNZ fail_state

    LDA #0x7FFF
    CLC
    NOP
    JCC .nop_7fff_ok
    JMP fail_state
.nop_7fff_ok:
    SUB #0x7FFF
    JNZ fail_state

    LDA #0x8000
    SEC
    NOP
    JCS .nop_8000_ok
    JMP fail_state
.nop_8000_ok:
    SUB #0x8000
    JNZ fail_state

    LDA #0xFFFF
    CLC
    NOP
    JCC .nop_ffff_c0_ok
    JMP fail_state
.nop_ffff_c0_ok:
    SUB #0xFFFF
    JNZ fail_state
    LDA #0xFFFF
    SEC
    NOP
    JCS .nop_ffff_c1_ok
    JMP fail_state
.nop_ffff_c1_ok:
    SUB #0xFFFF
    JNZ fail_state

    ; SEC must preserve representative accumulator values and force C=1.
    LDA #0x0000
    SEC
    JCS .sec_0_ok
    JMP fail_state
.sec_0_ok:
    SUB #0x0000
    JNZ fail_state
    LDA #0x0001
    SEC
    JCS .sec_1_ok
    JMP fail_state
.sec_1_ok:
    SUB #0x0001
    JNZ fail_state
    LDA #0x7FFF
    SEC
    JCS .sec_7fff_ok
    JMP fail_state
.sec_7fff_ok:
    SUB #0x7FFF
    JNZ fail_state
    LDA #0x8000
    SEC
    JCS .sec_8000_ok
    JMP fail_state
.sec_8000_ok:
    SUB #0x8000
    JNZ fail_state
    LDA #0xFFFF
    SEC
    JCS .sec_ffff_ok
    JMP fail_state
.sec_ffff_ok:
    SUB #0xFFFF
    JNZ fail_state

    ; CLC consumes either input state, preserves A, and leaves C clear.
    LDA #0x1234
    CLC
    CLC
    JCC .clc_c0_ok
    JMP fail_state
.clc_c0_ok:
    SUB #0x1234
    JNZ fail_state
    LDA #0xABCD
    SEC
    CLC
    JCC .clc_c1_ok
    JMP fail_state
.clc_c1_ok:
    SUB #0xABCD
    JNZ fail_state

    ; LDC must load the operand without changing either input carry state.
    LDC #0x0000
    CLC
    LDC #0x0000
    JCC .ldc_0_c0_ok
    JMP fail_state
.ldc_0_c0_ok:
    SUB #0x0000
    JNZ fail_state
    SEC
    LDC #0x0000
    JCS .ldc_0_c1_ok
    JMP fail_state
.ldc_0_c1_ok:
    SUB #0x0000
    JNZ fail_state

    CLC
    LDC #0x0001
    JCC .ldc_1_c0_ok
    JMP fail_state
.ldc_1_c0_ok:
    SUB #0x0001
    JNZ fail_state
    SEC
    LDC #0x0001
    JCS .ldc_1_c1_ok
    JMP fail_state
.ldc_1_c1_ok:
    SUB #0x0001
    JNZ fail_state

    CLC
    LDC #0x7FFF
    JCC .ldc_7fff_c0_ok
    JMP fail_state
.ldc_7fff_c0_ok:
    SUB #0x7FFF
    JNZ fail_state
    SEC
    LDC #0x7FFF
    JCS .ldc_7fff_c1_ok
    JMP fail_state
.ldc_7fff_c1_ok:
    SUB #0x7FFF
    JNZ fail_state

    CLC
    LDC #0x8000
    JCC .ldc_8000_c0_ok
    JMP fail_state
.ldc_8000_c0_ok:
    SUB #0x8000
    JNZ fail_state
    SEC
    LDC #0x8000
    JCS .ldc_8000_c1_ok
    JMP fail_state
.ldc_8000_c1_ok:
    SUB #0x8000
    JNZ fail_state

    CLC
    LDC #0xFFFF
    JCC .ldc_ffff_c0_ok
    JMP fail_state
.ldc_ffff_c0_ok:
    SUB #0xFFFF
    JNZ fail_state
    SEC
    LDC #0xFFFF
    JCS .ldc_ffff_c1_ok
    JMP fail_state
.ldc_ffff_c1_ok:
    SUB #0xFFFF
    JNZ fail_state

    LDA testGuard
    SUB #0x5A5A
    JNZ fail_memory
    RET

fail_state:
    MOV HEX_DISPLAY, #ERR_STATE
    HALT

fail_memory:
    MOV HEX_DISPLAY, #ERR_MEMORY
    HALT

;---------------------------------------------------------------
macro_logic_test:
    MOV testGuard, #0x5A5A

    ; OR and accumulator NOT preserve carry.
    LDA #0x000F
    SEC
    OR #0x0010               ; 0x000F | 0x0010 = 0x001F
    JCS .or_cf_ok
    JMP fail_logic_cf
.or_cf_ok:
    SUB #0x001F
    JNZ fail_logic

    LDA #0x001F
    SEC
    NOT
    NOT
    JCS .not_cf_ok
    JMP fail_logic_cf
.not_cf_ok:
    SUB #0x001F
    JNZ fail_logic

    ; Operand NOT loads through LDA and therefore finishes with carry clear.
    MOV testValueA, #0x0003
    SEC
    NOT testValueA
    JCC .not_operand_cf_ok
    JMP fail_logic_cf
.not_operand_cf_ok:
    SUB #0xFFFC
    JNZ fail_logic
    LDA testValueA           ; source memory must be unchanged
    SUB #0x0003
    JNZ fail_memory

    ; AND/NAND use RPAR and finish with carry clear after their internal LDA.
    LDA #0x000F
    SEC
    AND #0x000A
    JCC .and_cf_ok
    JMP fail_logic_cf
.and_cf_ok:
    SUB #0x000A
    JNZ fail_logic
    LDA #0x0003
    SEC
    NAND #0x0003
    JCC .nand_cf_ok
    JMP fail_logic_cf
.nand_cf_ok:
    SUB #0xFFFC
    JNZ fail_logic

    ; Required XOR regressions. XOR clobbers RPAR/RPAR+1 and finishes C=0.
    LDA #0x0000
    XOR #0x0000
    JCC .xor_00_ok
    JMP fail_logic_cf
.xor_00_ok:
    JNZ fail_logic
    LDA #0x0000
    XOR #0xFFFF
    STA testActual
    JCC .xor_0f_ok
    JMP fail_logic_cf
.xor_0f_ok:
    LDA testActual
    SUB #0xFFFF
    JNZ fail_logic
    LDA #0xFFFF
    XOR #0xFFFF
    JNZ fail_logic
    LDA #0xAAAA
    XOR #0x5555
    SUB #0xFFFF
    JNZ fail_logic
    LDA #0x8000
    XOR #0x0001
    SUB #0x8001
    JNZ fail_logic
    LDA #0x1234
    XOR #0xABCD
    SUB #0xB9F9
    JNZ fail_logic

    LDA testGuard
    SUB #0x5A5A
    JNZ fail_memory

    RET

fail_logic:
    MOV HEX_DISPLAY, #ERR_LOGIC
    HALT

fail_logic_cf:
    MOV HEX_DISPLAY, #ERR_LOGIC_CF
    HALT

;---------------------------------------------------------------
macro_math_test:
    ; INC: carry-out only on 0xFFFF.
    LDA #0x0000
    INC
    JCC .inc_0_cf_ok
    JMP fail_math_cf
.inc_0_cf_ok:
    SUB #0x0001
    JNZ fail_math
    LDA #0xFFFE
    INC
    JCC .inc_fffe_cf_ok
    JMP fail_math_cf
.inc_fffe_cf_ok:
    SUB #0xFFFF
    JNZ fail_math
    LDA #0xFFFF
    INC
    JCS .inc_ffff_cf_ok
    JMP fail_math_cf
.inc_ffff_cf_ok:
    JNZ fail_math

    ; DEC: C=1 for a non-zero input, C=0 only for input zero.
    LDA #0x0001
    DEC
    JCS .dec_1_cf_ok
    JMP fail_math_cf
.dec_1_cf_ok:
    JNZ fail_math
    LDA #0x0000
    DEC
    JCC .dec_0_cf_ok
    JMP fail_math_cf
.dec_0_cf_ok:
    SUB #0xFFFF
    JNZ fail_math
    LDA #0xFFFF
    DEC
    JCS .dec_ffff_cf_ok
    JMP fail_math_cf
.dec_ffff_cf_ok:
    SUB #0xFFFE
    JNZ fail_math

    ; NEG: C=1 only for zero, otherwise C=0.
    LDA #0x0000
    NEG
    JCS .neg_0_cf_ok
    JMP fail_math_cf
.neg_0_cf_ok:
    JNZ fail_math
    LDA #0x0001
    NEG
    JCC .neg_1_cf_ok
    JMP fail_math_cf
.neg_1_cf_ok:
    SUB #0xFFFF
    JNZ fail_math
    LDA #0x7FFF
    NEG
    JCC .neg_7fff_cf_ok
    JMP fail_math_cf
.neg_7fff_cf_ok:
    SUB #0x8001
    JNZ fail_math
    LDA #0x8000
    NEG
    JCC .neg_8000_cf_ok
    JMP fail_math_cf
.neg_8000_cf_ok:
    SUB #0x8000
    JNZ fail_math
    LDA #0xFFFF
    NEG
    JCC .neg_ffff_cf_ok
    JMP fail_math_cf
.neg_ffff_cf_ok:
    SUB #0x0001
    JNZ fail_math

    ; SUB convention: C=1 exactly when an unsigned borrow is needed.
    LDA #0x0005
    SUB #0x0003
    JCC .sub_5_3_cf_ok
    JMP fail_math_cf
.sub_5_3_cf_ok:
    SUB #0x0002
    JNZ fail_math
    LDA #0x0003
    SUB #0x0005
    JCS .sub_3_5_cf_ok
    JMP fail_math_cf
.sub_3_5_cf_ok:
    SUB #0xFFFE
    JNZ fail_math
    LDA #0x0000
    SUB #0x0000
    JCC .sub_0_0_cf_ok
    JMP fail_math_cf
.sub_0_0_cf_ok:
    JNZ fail_math
    LDA #0x0000
    SUB #0x0001
    JCS .sub_0_1_cf_ok
    JMP fail_math_cf
.sub_0_1_cf_ok:
    SUB #0xFFFF
    JNZ fail_math
    LDA #0xFFFF
    SUB #0x0001
    JCC .sub_ffff_1_cf_ok
    JMP fail_math_cf
.sub_ffff_1_cf_ok:
    SUB #0xFFFE
    JNZ fail_math
    LDA #0x8000
    SUB #0x7FFF
    JCC .sub_8000_7fff_cf_ok
    JMP fail_math_cf
.sub_8000_7fff_cf_ok:
    SUB #0x0001
    JNZ fail_math
    RET

fail_math:
    MOV HEX_DISPLAY, #ERR_MATH
    HALT

fail_math_cf:
    MOV HEX_DISPLAY, #ERR_MATH_CF
    HALT

;---------------------------------------------------------------
macro_stack_test:
    PUSH #0x0055
    PUSH #0x00AA
    POP  testValueA
    POP
    SUB #0x0055
    JNZ fail_stack
    LDA testValueA
    SUB #0x00AA
    JNZ fail_stack
    RET

fail_stack:
    MOV HEX_DISPLAY, #ERR_STACK
    HALT

;---------------------------------------------------------------
macro_branch_test:
    ; unconditionnal jumps
    JMP .jmp_dest
    MOV HEX_DISPLAY, #ERR_BRANCH
    HALT

.jmp_dest:
    CLR
    INC                       ; A = 1
    STA R0
    JZ  branch_fail
    LDA R0

    CLR
    STA R0
    JNZ branch_fail
    LDA R0

    JZ  branch_ok

branch_fail:
    MOV HEX_DISPLAY, #ERR_BRANCH
    HALT
branch_ok:
    RET

;---------------------------------------------------------------
macro_shift_test:
    ; --- 1. LSL : 0x0001 → 0x0002 -----------------------------
    LDA #1
    LSL
    STA R0
    SUB #2
    JNZ fail_shift
    LDA R0                ; restore A = 0x0002

    ; --- 2. ROL : 0x0002 → 0x0004 (carry cleared) ------------
    CLC                   ; predictable carry = 0
    ROL
    STA R0
    SUB #4
    JNZ fail_shift
    LDA R0                ; restore A = 0x0004

    ; --- 3. ROR : 0x0004 → 0x0002 -----------------------------
    CLC
    ROR
    STA R0
    SUB #2
    JNZ fail_shift
    LDA R0                ; restore A = 0x0002

    ; --- 4. LSR : 0x0002 → 0x0001 -----------------------------
    LSR
    SUB #1
    JNZ fail_shift
    
    ; --- 5. LSR : 0x0003 → 0x0001 -----------------------------
    LSR #3
    SUB #1
    JNZ fail_shift

    ; --- 6. 4x ROL : 0x3000 → 0x0003 -----------------------------
    ROL TEST_VALUE
    ROL
    ROL
    ROL
    STA R0
    SUB #3
    JNZ fail_shift

    ; --- 7. 4x ROR : 0x0003 → 0x300 -----------------------------
    ROR R0
    ROR
    ROR
    ROR
    SUB TEST_VALUE
    JNZ fail_shift

    ; LSL exposes the old bit 15 as carry.
    LDA #0x7FFF
    LSL
    STA testActual
    JCC .lsl_7fff_cf_ok
    JMP fail_shift
.lsl_7fff_cf_ok:
    LDA testActual
    SUB #0xFFFE
    JNZ fail_shift
    LDA #0x8000
    LSL
    STA testActual
    JCS .lsl_8000_cf_ok
    JMP fail_shift
.lsl_8000_cf_ok:
    LDA testActual
    JNZ fail_shift

    ; ROL returns the rotated value but deliberately leaves carry clear.
    LDA #0x8000
    SEC
    ROL
    STA testActual
    JCC .rol_cf_ok
    JMP fail_shift
.rol_cf_ok:
    LDA testActual
    SUB #0x0001
    JNZ fail_shift

    ; Right operations exercise low/high bits and alternating patterns.
    ROR #0x0001
    SUB #0x8000
    JNZ fail_shift
    ROR #0x8001
    SUB #0xC000
    JNZ fail_shift
    ROR #0xAAAA
    SUB #0x5555
    JNZ fail_shift
    LSR #0x0001
    JNZ fail_shift
    LSR #0x8001
    SUB #0x4000
    JNZ fail_shift
    LSR #0xFFFF
    SUB #0x7FFF
    JNZ fail_shift
    LSR #0x5555
    SUB #0x2AAA
    JNZ fail_shift
    RET

fail_shift:
    MOV HEX_DISPLAY, #ERR_SHIFT
    HALT

TEST_VALUE: #d16 0x3000

;---------------------------------------------------------------
macro_adc_test:
    ; ADC/SBC value and flag failures deliberately use distinct error codes.
    ; Required ADC vectors: (A, B, C_in) -> (value, C_out).
    LDA #0xFFFF
    SEC
    ADC #0x0000              ; -> 0000,1
    STA testActual
    JCS .adc_a_ok
    JMP fail_adc_cf
.adc_a_ok:
    LDA testActual
    JNZ fail_adc_value
    LDA RPAR                 ; declared clobber contains the result
    JNZ fail_adc_value

    LDA #0xFFFF
    SEC
    ADC #0x0001              ; -> 0001,1
    STA testActual
    JCS .adc_b_ok
    JMP fail_adc_cf
.adc_b_ok:
    LDA testActual
    SUB #0x0001
    JNZ fail_adc_value

    LDA #0xFFFF
    SEC
    ADC #0x1234              ; -> 1234,1
    STA testActual
    JCS .adc_c_ok
    JMP fail_adc_cf
.adc_c_ok:
    LDA testActual
    SUB #0x1234
    JNZ fail_adc_value

    LDA #0xFFFE
    SEC
    ADC #0x0001              ; -> 0000,1
    STA testActual
    JCS .adc_d_ok
    JMP fail_adc_cf
.adc_d_ok:
    LDA testActual
    JNZ fail_adc_value

    LDA #0x0000
    SEC
    ADC #0xFFFF              ; -> 0000,1
    STA testActual
    JCS .adc_e_ok
    JMP fail_adc_cf
.adc_e_ok:
    LDA testActual
    JNZ fail_adc_value

    LDA #0x8000
    CLC
    ADC #0x8000              ; -> 0000,1
    STA testActual
    JCS .adc_f_ok
    JMP fail_adc_cf
.adc_f_ok:
    LDA testActual
    JNZ fail_adc_value

    ; Required SBC vectors all use input borrow one.
    LDA #0x0000
    SEC
    SBC #0x0000              ; -> FFFF,1
    STA testActual
    JCS .sbc_a_ok
    JMP fail_sbc_cf
.sbc_a_ok:
    LDA testActual
    SUB #0xFFFF
    JNZ fail_sbc_value
    LDA RPAR
    SUB #0xFFFF
    JNZ fail_sbc_value

    LDA #0x0000
    SEC
    SBC #0x0001              ; -> FFFE,1
    STA testActual
    JCS .sbc_b_ok
    JMP fail_sbc_cf
.sbc_b_ok:
    LDA testActual
    SUB #0xFFFE
    JNZ fail_sbc_value

    LDA #0x0000
    SEC
    SBC #0x1234              ; -> EDCB,1
    STA testActual
    JCS .sbc_c_ok
    JMP fail_sbc_cf
.sbc_c_ok:
    LDA testActual
    SUB #0xEDCB
    JNZ fail_sbc_value

    LDA #0x0001
    SEC
    SBC #0x0000              ; -> 0000,0
    STA testActual
    JCC .sbc_d_ok
    JMP fail_sbc_cf
.sbc_d_ok:
    LDA testActual
    JNZ fail_sbc_value

    LDA #0xFFFF
    SEC
    SBC #0xFFFF              ; -> FFFF,1
    STA testActual
    JCS .sbc_e_ok
    JMP fail_sbc_cf
.sbc_e_ok:
    LDA testActual
    SUB #0xFFFF
    JNZ fail_sbc_value

    LDA #0x8000
    SEC
    SBC #0x7FFF              ; -> 0000,0
    STA testActual
    JCC .sbc_f_ok
    JMP fail_sbc_cf
.sbc_f_ok:
    LDA testActual
    JNZ fail_sbc_value
    RET

fail_adc_value:
    MOV HEX_DISPLAY, #ERR_ADC_VALUE
    HALT
fail_adc_cf:
    MOV HEX_DISPLAY, #ERR_ADC_CF
    HALT
fail_sbc_value:
    MOV HEX_DISPLAY, #ERR_SBC_VALUE
    HALT
fail_sbc_cf:
    MOV HEX_DISPLAY, #ERR_SBC_CF
    HALT

;---------------------------------------------------------------
macro_stack_idx_test:
    PUSH #0x0555
    PUSH #0x0222
    PUSH #0x0333
    LDS #2
    SUB #0x0222
    JNZ fail_stackidx
    STS #1, #0x06AA
    LDS #1
    SUB #0x06AA
    JNZ fail_stackidx
    POP
    POP
    POP
    RET
fail_stackidx:
    MOV HEX_DISPLAY, #ERR_STACKIDX
    HALT

;---------------------------------------------------------------
;  core_carry_test
;  1) Carry must survive a non-arithmetic instruction (STA)
;  2) Carry must be re-set by a new ADD overflow
;---------------------------------------------------------------
core_carry_test:
    ; --- Step A : create Carry = 1 ---------------------------
    CLR                 ; A = 0
    NOR #0              ; A = 0xFFFF
    ADD #1              ; A = 0, Carry = 1

    ; --- Step B : a non-ALU op must NOT alter Carry ----------
    STA R0              ; store A, Carry should stay 1
    JCC fail_carry_hold ; if branch -> Carry was erased
    ; (JCC didn't branch, but has just cleared Carry by spec)

    ; --- Step C : overflow again -> Carry must be set --------
    CLR
    NOR #0              ; A = 0xFFFF
    ADD #1              ; Carry should become 1
    STA R0              ; neutral op, must keep Carry = 1
    JCS carry_ok        ; must jump; if not, Carry not updated    

    ; --- Step D : ADD update Carry --------
    CLR
    NOR #0              ; A = 0xFFFF
    ADD #1              ; Carry should become 1 (as step A)
    ADD #0              ; Carry should be updated to 0
    JCC carry_ok        ; must jump; if not, Carry not updated
fail_carry_upd:
    MOV HEX_DISPLAY, #ERR_CARRY_UPD
    HALT

carry_ok:
    RET

fail_carry_hold:
    MOV HEX_DISPLAY, #ERR_CARRY
    HALT
