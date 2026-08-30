; =============================================================================
; S-CPU SAMPLE - ADDRESSING MODES
; =============================================================================
; What it does:
;   Stores 42 in RAM, saves its address in another RAM word, then reads the
;   value back through that pointer.
; Expected result:
;   The hexadecimal display shows 42 (0x002A). In the debugger, `value` holds
;   42, `copy` holds 42, and `pointer` holds the address of `copy`.
; Runs on:
;   Desktop/CLI simulators and targets exposing the device-0 display.
; Demonstrates:
;   Immediate, direct RAM, address, and indirect addressing.
; =============================================================================

; IODEV is the MMIO base; offset 1 selects device 0's hexadecimal display.
#const HEX_DISPLAY = IODEV+1

; #res counts 16-bit words in RAM. Each `#res 1` below therefore reserves one
; word for one variable; it does not place initial data in the ROM.
#bank userpage
value:   #res 1
copy:    #res 1
pointer: #res 1

#bank prg

start:
  mov value, #42       ; immediate: literal 42 -> RAM[value]
  mov copy, value      ; direct: RAM[value] -> RAM[copy]
  mov pointer, #copy   ; address: `#copy` is the address of RAM[copy]
  lda @pointer         ; indirect: A = RAM[RAM[pointer]]
  sta HEX_DISPLAY
  halt
