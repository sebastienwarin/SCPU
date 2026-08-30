; =============================================================================
; S-CPU SAMPLE - LCD2004
; =============================================================================
; What it does:
;   Initializes an HD44780-compatible LCD2004 and repeatedly writes a greeting.
; Expected result:
;   The physical LCD displays "Hello, S-CPU!", clears, and repeats.
; Runs on:
;   S-CPU TTL with the LCD2004 connected to device 2.
; Real hardware required:
;   Yes. The LCD controller is not emulated by the software simulators.
; Demonstrates:
;   LCD commands/data, initialization timing, strings, and reusable delays.
; =============================================================================

#include "common/Delay.asm"

; IODEV is the MMIO base. Adding 0x200 selects physical device 2: register 1
; sends LCD commands and register 2 sends LCD character data.
; BIT1=ENABLE & BIT0=RS (LOW = COMMAND, HIGH = DATA)
#const LCD_COMMAND = IODEV+0x201
#const LCD_DATA = IODEV+0x202
#const LED_BANK = IODEV+2

#bank userpage

; Each #res 1 reserves one 16-bit RAM word for the demo's working state.
pauseMilliseconds: #res 1
stringPointer: #res 1
currentCharacter: #res 1

#bank prg

start:

  MOV pauseMilliseconds, #10 ; 10ms

  ; Device 0 register 2 is the LED bank, used here as a visible progress marker.
  mov LED_BANK, #1
  push #2000
  call delay
  pop

  ; Init LCD screen - first try  
  mov LED_BANK, #2
  call init_display
  push #1500
  call delay
  pop

  ; second try
  mov LED_BANK, #3
  call init_display
  call pause
  mov LED_BANK, #4

  ; third go
  mov LED_BANK, #5
  call init_display
  call pause

  loop:
  MOV pauseMilliseconds, #10

  ; Config display
  mov LED_BANK, #5
  call display_control
  call pause
  
  ; Clear display
  mov LED_BANK, #6
  call clear_display
  call pause

  ; Display hello world message
  MOV pauseMilliseconds, #100
  mov stringPointer, #(greetingText) ; address of the first greeting character
  .printchar:
    mov currentCharacter, @(stringPointer) ; read the current greeting character
    jz .break           ; break if currentCharacter=='\0'
    inc stringPointer
    sta stringPointer             ; stringPointer++
    push #1
    push currentCharacter
    call write_lcd
    pop
    pop
    call pause          ; pause
    jmp .printchar
  .break:

  ; Wait and repeat.
  mov LED_BANK, #8
  push #2000
  call delay
  pop
  jmp loop
  
halt

greetingText: #d "Hello, S-CPU!", 0

pause:
  push pauseMilliseconds
  call delay
  pop
  ret

init_display:
  ; 0 0 1 D L N F — —
  push #0
  push #0b00111000
  call write_lcd
  pop
  pop
  ret

display_char:
  lds #2, R0
  push #1
  push R0
  call write_lcd
  pop
  pop
  ret

display_control:
  ; 0 0 0 0 1 D C B
  ; D: Sets entire display on/off
  ; C: cursor on/off
  ; B: blinking of cursor position
  push #0
  push #0b00001101  ; display and blinking enabled, cursor disabled
  call write_lcd
  pop
  pop
  ret

clear_display:
  push #0
  push #0b00000001
  call write_lcd
  pop
  pop
  ret

return_home:
  push #0
  push #0b00000010
  call write_lcd
  pop
  pop
  ret

; function write_lcd(uint8_t value, uint8_t mode)
  ; Bit1 = Enable (E)
  ;         LCD_COMMAND & 0x2 if enable
  ; Bit0 = Register Select (RS) : LOW = COMMAND, HIGH = DATA
  ;         LCD_COMMAND & 0x1 if DATA else COMMAND
write_lcd:
  ; Get arguments
  lds #2, R0 ; R0 = value
  lds #3, R1 ; R1 = mode (0 for command; 1 for data)

  ; Set Register select
  mov LCD_COMMAND, R1 ; RS=MODE & E=LOW
  nop
  nop

  ; Set data
  mov LCD_DATA, R0
  nop
  nop
  
  ; Pulse Enable
  lda R1
  add #2          ; E=HIGH
  sta LCD_COMMAND     ; E=HIGH & RS=MODE
  nop
  nop  
  mov LCD_COMMAND, R1 ; E=LOW & RS=MODE
  nop
  nop
  nop
  nop
  nop
  nop
  nop
  nop
  nop
  nop

  ret
