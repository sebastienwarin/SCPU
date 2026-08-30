; =============================================================================
; S-CPU SAMPLE - I2C BIT BANGING
; =============================================================================
; What it does:
;   Powers a TSL2561 sensor and reads its broadband and infrared channels using
;   a software-driven open-drain I2C bus.
; Expected result:
;   `broadbandValue` and `infraredValue` contain live 16-bit sensor readings in RAM.
; Runs on:
;   S-CPU TTL with device-3 SDA/SCL lines, pull-ups, and a TSL2561 at 0x39.
; Real hardware required:
;   Yes. The I2C bus and sensor are not emulated by the software simulators.
; Demonstrates:
;   START/STOP, byte transfers, ACK/NACK, bus timing, and register reads.
; =============================================================================

#include "common/Delay.asm"

; IODEV is the MMIO base. +0x300 selects physical device 3; register 1 exposes
; its I2C lines. Device 0 register 2 is available as an optional debug LED.
#const I2C_LINES = IODEV+0x301   ; bit 0 = SDA, bit 1 = SCL (write 1 to pull low)
#const LED_BANK = IODEV+2

#bank userpage
; Every #res 1 below reserves one 16-bit RAM word for transfer state or data.
ackReceived: #res 1

broadbandLow: #res 1
broadbandHigh: #res 1
broadbandValue: #res 1

infraredLow: #res 1
infraredHigh: #res 1
infraredValue: #res 1

#bank prg
start:

  ; == POWER ON ==
  ; I²C Start
  call i2c_start
  ; Send device address (write): 0x72 (ADDR floating = 0x39)
  mov R0, #0x72
  call send_byte
  ; Send register address: 0x80 (command bit + register 0x00)
  mov R0, #0x80
  call send_byte
  ; Send value: 0x03 (POWER ON)
  mov R0, #0x03
  call send_byte
  ; I²C stop
  call i2c_stop
  
  ; Integration time (default: 402 ms)
  ; TIMING (0x81) : 0x00 = 13.7ms, 0x01 = 101ms or 0x02 = 402ms (default)
  push #402
  call delay
  pop

  ; == READ DATA0LOW ==
  call i2c_start
  ; Device address WRITE (0x72)
  mov R0, #0x72
  call send_byte
  ; Register address with command bit (0x8C = 10001100b)
  mov R0, #0x8C
  call send_byte
  ; RE-START
  call i2c_start
  ; Device address READ (0x73)
  mov R0, #0x73
  call send_byte

  ; Read DATA0LOW, then ACK because three more bytes follow.
  push #0
  push #0 ; 0 = ack
  call read_byte
  pop
  pop broadbandLow

  ; Read DATA0HIGH and ACK.
  push #0
  push #0 ; 0 = ack
  call read_byte
  pop
  pop broadbandHigh

  ; Read DATA1LOW and ACK.
  push #0
  push #0 ; 0 = ack
  call read_byte
  pop
  pop infraredLow

  ; Read DATA1HIGH, then NACK to end the transfer.
  push #0
  push #1 ; 1 = nack
  call read_byte
  pop
  pop infraredHigh

  ; == STOP ==
  call i2c_stop

  ; Combine LSB/MSB for broadbandValue
  lda broadbandHigh
  lsl                    ; shift MSB to high 8 bits
  lsl
  lsl
  lsl
  lsl
  lsl
  lsl
  lsl
  or broadbandLow           ; combine with LSB
  sta broadbandValue

  ; Combine LSB/MSB for infraredValue
  lda infraredHigh
  lsl                    ; shift MSB to high 8 bits
  lsl
  lsl
  lsl
  lsl
  lsl
  lsl
  lsl
  or infraredLow           ; combine with LSB
  sta infraredValue

  ; Display lower 4 bits on LED_BANK (debug)
  sta LED_BANK

  ; Wait & loop
  ;push #2000
  ;call delay
  ;pop
  ;jmp start

  ; The accumulator still holds infraredValue when execution stops.
  halt

; ---- Subroutines ----

; Read a byte, send ACK/NACK
read_byte:
  mov R2, #8
  mov R0, #0         ; Will store result here

.rd_loop:

  ; Release SDA (set SDA=HIGH, SCL=LOW) 
  mov I2C_LINES, #2
  call pause

  ; Raise SCL for ACK (SCL=HIGH, SDA=HIGH)
  mov I2C_LINES, #0
  call pause

  ; Read SDA
  lda I2C_LINES
  and #1
  sta R1            ; Save SDA to R1
  lsl R0            ; Shift result left
  or R1             ; OR with SDA bit
  sta R0
  
  ; Lower SCL
  mov I2C_LINES, #2
  call pause

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
  call pause
  ; Raise SCL for ACK (SCL=HIGH, SDA=HIGH)
  mov I2C_LINES, #0
  call pause
  ; Lower SCL
  mov I2C_LINES, #2
  call pause
  jmp .exit
  
  ; 0 = Send ACK  (SDA=LOW, SCL=LOW)
  .ack:
  mov I2C_LINES, #3
  call pause
  ; Raise SCL for ACK (SCL=HIGH, SDA=HIGH)
  mov I2C_LINES, #1
  call pause
  ; Lower SCL
  mov I2C_LINES, #3
  call pause

  ; Return result
  .exit:
  sts #3, R0
  ret

; send_byte: send byte in R0, read ACK into ackReceived
send_byte:

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
  call pause

  ; Raise SCL (set bit1)
  lda R3
  and #1
  sta R3
  sta I2C_LINES
  call pause

  ; Lower SCL (clear bit1)
  lda R3
  or #2
  sta I2C_LINES
  call pause

  ; Decrement bit count & loop
  dec R2
  sta R2
  jnz .send_loop

  ; Release SDA (set SDA=HIGH, SCL=LOW) 
  mov I2C_LINES, #2
  call pause

  ; Raise SCL for ACK (SCL=HIGH, SDA=HIGH)
  mov I2C_LINES, #0
  call pause

  ; Read SDA bit for ACK (ACK = 0 means OK)
  lda I2C_LINES
  and #1
  sta ackReceived

  sta LED_BANK      ; Debug aid: display the ACK bit on the LED bank.

  ; Lower SCL
  mov I2C_LINES, #2
  call pause
  ret

; i2c_start: SDA falling while SCL high
i2c_start:
  mov I2C_LINES, #0       ; Both released (SCL=HIGH, SDA=HIGH)
  call pause
  
  mov I2C_LINES, #1       ; SDA=LOW, SCL=HIGH (start condition: SDA falls while SCL is HIGH)
  call pause
  
  mov I2C_LINES, #3       ; SDA=LOW, SCL=LOW
  call pause
  ret

; i2c_stop: SDA rising while SCL high
i2c_stop:
  mov I2C_LINES, #3       ; SDA=LOW, SCL=LOW
  call pause

  mov I2C_LINES, #1       ; SDA=LOW, SCL=HIGH
  call pause

  mov I2C_LINES, #0       ; Both released (SCL=HIGH, SDA=HIGH, stop condition: SDA rises while SCL is HIGH)
  call pause
  ret

pause:
  nop
  nop
  ret
