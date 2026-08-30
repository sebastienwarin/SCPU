; BIT1=ENABLE & BIT0=RS (LOW = COMMAND, HIGH = DATA)
#const LCD_COMMAND = IODEV+0x201
#const LCD_DATA = IODEV+0x202

lcd_begin:
  push #2000
  call delay
  pop
  ; Init LCD screen - first try
  call lcd_init
  push #1500
  call delay
  pop
  ; second try
  call lcd_init  
  push #150
  call delay
  pop
  ; third go
  call lcd_init
  ret

lcd_init:
  ; 0 0 1 D L N F — —
  push #0
  push #0b00111000
  call lcd_write
  pop
  pop
  call lcd_pause
  ret

lcd_print:
  lds #2, R0
  push #1
  push R0
  call lcd_write
  pop
  pop
  call lcd_pause
  ret

lcd_control:
  ; 0 0 0 0 1 D C B
  ; D: Sets entire display on/off
  ; C: cursor on/off
  ; B: blinking of cursor position
  push #0
  push #0b00001101  ; display and blinking enabled, cursor disabled
  call lcd_write
  pop
  pop
  call lcd_pause
  ret

lcd_clear:
  push #0
  push #0b00000001
  call lcd_write
  pop
  pop
  call lcd_pause
  ret

lcd_home:
  push #0
  push #0b00000010
  call lcd_write
  pop
  pop
  call lcd_pause
  ret

lcd_pause:
  push #10
  call delay
  pop
  ret

; function write_lcd(uint8_t value, uint8_t mode)
  ; Bit1 = Enable (E)
  ;         LCD_COMMAND & 0x2 if enable
  ; Bit0 = Register Select (RS) : LOW = COMMAND, HIGH = DATA
  ;         LCD_COMMAND & 0x1 if DATA else COMMAND
lcd_write:
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
