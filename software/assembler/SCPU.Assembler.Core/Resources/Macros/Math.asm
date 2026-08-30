[macro inc]
add #1

[macro inc {operand}]
lda {operand}
inc

[macro dec]
add MAX_VALUE

[macro dec {operand}]
lda {operand}
dec

[macro neg]
not
add #1

[macro neg {operand}]
lda {operand}
neg

[macro sub {operand}]
not
add {operand}
not

[macro adc {operand}]
jcc __adc_add_{uid}       ; No input carry: add operand directly
inc                       ; Add the input carry
jcc __adc_add_{uid}       ; INC did not wrap: ADD will produce the final carry
lda {operand}             ; INC wrapped 0xFFFF to 0x0000: result is operand
sta RPAR                  ; Save the result before restoring the carry
jmp __adc_set_{uid}
__adc_add_{uid}:
add {operand}             ; Compute the result and output carry
sta RPAR                  ; Save the result before testing the carry
jcs __adc_set_{uid}       ; Output carry set: restore it after reloading ACC
lda RPAR                  ; Restore the result with carry clear
jmp __adc_end_{uid}
__adc_set_{uid}:
lda RPAR                  ; Restore the result
sec                       ; Restore the output carry
__adc_end_{uid}:

[macro sbc {operand}]
jcc __sbc_sub_{uid}       ; No input borrow: subtract operand directly
dec                       ; Subtract the input borrow
jcc __sbc_wrap_{uid}      ; DEC wrapped 0x0000 to 0xFFFF
__sbc_sub_{uid}:
sub {operand}             ; Compute the result and output borrow
sta RPAR                  ; Save the result before testing the borrow
jcs __sbc_set_{uid}       ; Output borrow set: restore it after reloading ACC
lda RPAR                  ; Restore the result with borrow clear
jmp __sbc_end_{uid}
__sbc_wrap_{uid}:
sub {operand}             ; Complete 0x0000 - operand - 1
sta RPAR                  ; Save the wrapped result
__sbc_set_{uid}:
lda RPAR                  ; Restore the result
sec                       ; Restore the output borrow
__sbc_end_{uid}:

[macro ldc {operand}]
clr
nor {operand}
not                       ; ACC = operand, carry unchanged

[macro clc]
jcc $+1

[macro sec]
sta CF