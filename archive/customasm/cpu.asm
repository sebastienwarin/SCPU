#include "constants.asm"

#bankdef header   { #bits 16, #addr 0x0, #size 0x80, #outp 16 * 0x0 }
#bankdef consts   { #bits 16, #addr 0x80, #size 0x80, #outp 16 * 0x80 }
#bankdef prg      { #bits 16, #addr ENTRY_POINT, #size 0xFF00, #outp 16 * 0x100 }
#bankdef zeropage { #bits 16, #addr ZEROPAGE, #size 0x100 }
#bankdef userpage { #bits 16, #addr USERPAGE, #size 0x600 }
#bankdef resvpage { #bits 16, #addr RESVPAGE, #size 0x100 }

#include "instructions.asm"
#include "macros/common.asm"
#include "macros/logic.asm"
#include "macros/math.asm"
#include "macros/stack.asm"

#bank header

; Init Stack Pointer
lda RAMBASE
add #0xFF
sta SP      ; mem[SP] = 0x20FF
clr

; Start program
jmp ENTRY_POINT
