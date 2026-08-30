[macro not]
nor #0

[macro not {operand}]
lda {operand}
not

[macro and {operand}]
not
sta RPAR
lda {operand}
not
nor RPAR

[macro nand {operand}]
and {operand}
not

[macro or {operand}]
nor {operand}
not

[macro xor {operand}]
sta RPAR        ; Save A
nor {operand}   ; N = A NOR B
sta RPAR+1
lda RPAR        ; Reload A
nor RPAR+1      ; X = A NOR N
sta RPAR        ; Original A no longer needed
lda {operand}   ; Load B
nor RPAR+1      ; Y = B NOR N
nor RPAR        ; XNOR = X NOR Y
not             ; XOR

[macro lsl]
sta RPAR
add RPAR

[macro lsl {operand}]
lda {operand}
add {operand}

[macro rol]
lsl
jcc $+2
inc

[macro rol {operand}]
lda {operand}
rol

[macro ror]
rol
rol
rol
rol
rol
rol
rol
rol
rol
rol
rol
rol
rol
rol
rol

[macro ror {operand}]
lda {operand}
ror

[macro lsr]
ror
and #0x7FFF

[macro lsr {operand}]
lda {operand}
lsr