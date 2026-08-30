; ---------------------------------------------------------
; PrintNumberToBuffer
;   Converts the unsigned integer in R0 to a decimal ASCII string,
;   storing the result in the buffer pointed to by R1.
;   The string is null-terminated after the call (not in this routine).
;   Uses recursion, PUSH/POP for digit handling.
;   Destroys R0-R3 and uses the accumulator (A).
; ---------------------------------------------------------
PrintNumberToBuffer:
    LDA R0
    SUB #10
    JCS .last_digit     ; If R0 < 10, end recursion and print digit

    ; --- Manual division: R0 / 10 ---
    MOV R2, #0          ; quotient = 0
    LDA R0
    STA R3              ; remainder = value to be reduced
.div_loop:
    LDA R3
    SUB #10
    JCS .div_done
    STA R3
    INC R2
    STA R2              ; Save incremented quotient to R2
    LDA R3
    JMP .div_loop
.div_done:
    ; R2 = quotient (for recursion), R3 = remainder (to print after)
    MOV R0, R2          ; Prepare R0 for recursion
    PUSH R3             ; Save remainder before recursion
    CALL PrintNumberToBuffer
    POP R3              ; Restore remainder (current digit)

    LDA R3
    ADD #0x30
    STA @(R1)           ; Write ASCII digit to buffer
    INC R1
    STA R1              ; Update buffer pointer
    RET

.last_digit:
    LDA R0
    ADD #0x30
    STA @(R1)           ; Write last ASCII digit to buffer
    INC R1
    STA R1              ; Update buffer pointer
    RET
