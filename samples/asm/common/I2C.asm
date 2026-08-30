; Open-drain MMIO interface addr. 0x2B01
#const I2C_LINES = IODEV+0x301   ; BIT0 = SDA (pull LOW if 1), BIT1 = SCL (pull LOW if 1)

; byte i2c_read_byte(byte data) // return data
i2c_read_byte:
  mov R2, #8
  mov R0, #0         ; Will store result here

.rd_loop:
  ; Release SDA (set SDA=HIGH, SCL=LOW) 
  mov I2C_LINES, #2
  call i2c_pause

  ; Raise SCL for ACK (SCL=HIGH, SDA=HIGH)
  mov I2C_LINES, #0
  call i2c_pause

  ; Read SDA
  lda I2C_LINES
  and #1
  sta R1            ; Save SDA to R1
  lsl R0            ; Shift result left
  or R1             ; OR with SDA bit
  sta R0
  
  ; Lower SCL
  mov I2C_LINES, #2
  call i2c_pause

  ; Decrement counter
  dec R2
  sta R2
  jnz .rd_loop
  
  ; Send ack or nack
  lds #2
  jz .ack

  ; 1 = Send NACK (set SDA=HIGH, SCL=LOW) 
  .nack:
  mov I2C_LINES, #2
  call i2c_pause
  ; Raise SCL for ACK (SCL=HIGH, SDA=HIGH)
  mov I2C_LINES, #0
  call i2c_pause
  ; Lower SCL
  mov I2C_LINES, #2
  call i2c_pause
  jmp .exit
  
  ; 0 = Send ACK  (SDA=LOW, SCL=LOW)
  .ack:
  mov I2C_LINES, #3
  call i2c_pause
  ; Raise SCL for ACK (SCL=HIGH, SDA=HIGH)
  mov I2C_LINES, #1
  call i2c_pause
  ; Lower SCL
  mov I2C_LINES, #3
  call i2c_pause

  ; Return result
  .exit:
  sts #3, R0
  ret

; byte i2c_send_byte(byte data) // return ack
i2c_send_byte:
  lds #2, R0

  ; Pre-align the data: put the byte in the upper 8 bits
  lsl R0
  lsl
  lsl
  lsl
  lsl
  lsl
  lsl
  lsl
  sta R0

  mov R2, #8      ; Bit counter (8 bits to send)
.send_loop:
  lsl R0          ; Shift out MSB to Carry
  sta R0
  jcc .bit0
  mov R3, #0      ; SDA released (HIGH)
  jmp .bit_done
.bit0:
  mov R3, #1      ; SDA pulled LOW (bit0 = 1)
.bit_done:
  add #2          ; Keep SCL LOW
  sta R3
  sta I2C_LINES          ; Set SDA
  call i2c_pause

  ; Raise SCL (set bit1)
  lda R3
  and #1
  sta R3
  sta I2C_LINES
  call i2c_pause

  ; Lower SCL (clear bit1)
  lda R3
  or #2
  sta I2C_LINES
  call i2c_pause

  ; Decrement bit count & loop
  dec R2
  sta R2
  jnz .send_loop

  ; Release SDA (set SDA=HIGH, SCL=LOW) 
  mov I2C_LINES, #2
  call i2c_pause

  ; Raise SCL for ACK (SCL=HIGH, SDA=HIGH)
  mov I2C_LINES, #0
  call i2c_pause

  ; Read SDA bit for ACK (ACK = 0 means OK)
  lda I2C_LINES
  and #1
  sts #3

  ; Lower SCL
  mov I2C_LINES, #2
  call i2c_pause
  ret

; i2c_start: SDA falling while SCL high
i2c_start:
  mov I2C_LINES, #0       ; Both released (SCL=HIGH, SDA=HIGH)
  call i2c_pause  
  mov I2C_LINES, #1       ; SDA=LOW, SCL=HIGH (start condition: SDA falls while SCL is HIGH)
  call i2c_pause  
  mov I2C_LINES, #3       ; SDA=LOW, SCL=LOW
  call i2c_pause
  ret

; i2c_stop: SDA rising while SCL high
i2c_stop:
  mov I2C_LINES, #3       ; SDA=LOW, SCL=LOW
  call i2c_pause
  mov I2C_LINES, #1       ; SDA=LOW, SCL=HIGH
  call i2c_pause
  mov I2C_LINES, #0       ; Both released (SCL=HIGH, SDA=HIGH, stop condition: SDA rises while SCL is HIGH)
  call i2c_pause
  ret

i2c_pause:
  nop
  nop
  ret
