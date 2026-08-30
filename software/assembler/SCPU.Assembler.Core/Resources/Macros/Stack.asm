[macro lds {index}]
lda SP
add {index}
sta RPEEK
lda @(RPEEK)

[macro lds {index}, {address}]
lds {index}
sta {address}

[macro sts {index}]
sta RPAR
lda SP
add {index}
sta RPEEK
lda RPAR
sta @(RPEEK)

[macro sts {index}, {operand}]
lda SP
add {index}
sta RPEEK
lda {operand}
sta @(RPEEK)

[macro pop]
inc SP
sta SP
lda @SP

[macro pop {operand}]
pop
sta {operand}

[macro push]
sta @SP
dec SP
sta SP

[macro push {operand}]
lda {operand}
push

[macro call {address}]
push #(__ret_{uid})
jmp {address}
__ret_{uid}:

[macro ret]
pop RRET
jmp @(RRET)