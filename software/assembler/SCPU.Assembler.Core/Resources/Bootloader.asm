#bank prg

; Init Stack Pointer
mov SP, #(RAM+0xFF)	; mem[SP] = 0x20FF
clr

; Start program
jmp ENTRY_POINT

; Global data constants
MAX_VALUE: #d16 0xFFFF

; User program entry point automatically injected by the assembler
; if not already defined by the user in their source code.
;ENTRY_POINT: