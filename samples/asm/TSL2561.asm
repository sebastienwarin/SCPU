; =============================================================================
; S-CPU SAMPLE - TSL2561 LIGHT METER
; =============================================================================
; What it does:
;   Reads a TSL2561 over I2C, converts the broadband value to decimal text, and
;   displays it on an LCD2004.
; Expected result:
;   The LCD shows "TSL2561 demo! Broadband:" followed by a changing value.
; Runs on:
;   S-CPU TTL with the device-3 I2C bus, TSL2561, and LCD2004.
; Real hardware required:
;   Yes. The sensor, bus, and LCD are not emulated by the software simulators.
; Demonstrates:
;   Composition of shared I2C, LCD, string conversion, and delay routines.
; =============================================================================

#include "common/Delay.asm"
#include "common/LCD2004.asm"
#include "common/I2C.asm"
#include "common/String.asm"

#bank userpage
; #res counts 16-bit RAM words. Scalars use one word; numberTextBuffer reserves
; seven consecutive words for the decimal text and its null terminator.
stringPointer: #res 1
currentCharacter: #res 1
tslAddress: #res 1

numberTextBuffer: #res 7   ; enough for "65535\0"

broadbandChannel: #res 1
infraredChannel: #res 1

#bank prg
start:
  ; Prepare address
  lsl #0x39 ; TSL2561_ADDR_FLOAT
  sta tslAddress

  ; Init LCD
  call lcd_begin

  .loop:
  ; Enable the TSL2561 device by setting the control bit to 0x03
  call enable
  
  ; Wait for the default 402 ms integration time.
  push #402
  call delay
  pop

  ; Read broadband (broadbandChannel)
  push #0
  push #0x8C  ; TSL2561_REGISTER_CHAN0_LOW
  call read16
  pop
  pop broadbandChannel

  ; Read IR (infraredChannel)
  push #0
  push #0x8E  ; TSL2561_REGISTER_CHAN1_LOW
  call read16
  pop
  pop infraredChannel

  ; Display result
  call lcd_control
  call lcd_clear

  push #(titleText)
  call print_string
  pop

  push #(broadbandLabel)
  call print_string
  pop

  ; PrintNumberToBuffer writes decimal characters starting at the address in R1.
  MOV R1, #(numberTextBuffer)

  ; Convert broadbandChannel to ASCII and append the string terminator.
  MOV R0, broadbandChannel
  CALL PrintNumberToBuffer
  LDA #0
  STA @(R1)      ; End of line

  ; Display the generated text.
  push #(numberTextBuffer)
  call print_string
  pop

  ; Wait & loop
  push #2000
  call delay
  pop
  jmp .loop
  ;halt

titleText: #d "TSL2561 demo!", 0
broadbandLabel: #d "Broadband: ", 0

print_string:
  lds #2, stringPointer
  .printchar:
    mov currentCharacter, @(stringPointer)       ; currentCharacter=*stringPointer[X]
    jz .break           ; break if currentCharacter=='\0'
    inc stringPointer
    sta stringPointer             ; stringPointer++
    push currentCharacter
    call lcd_print
    pop
    jmp .printchar
  .break:
  ret

enable:
  push #0x80 ; reg = 0x80 (command bit + register 0x00)
  push #0x03 ; value : 0x03 (POWER ON)
  call write8
  pop
  pop
  ret

read16:   ; uint16_t read16(uint8_t reg)
  push #0 ; lsb
  push #0 ; msb
  ; I²C start
  call i2c_start
  ; Send device address (write)
  push #0
  push tslAddress
  call i2c_send_byte
  pop
  pop
  ; Send register address
  lds #4, R0
  push #0
  push R0
  call i2c_send_byte
  pop
  pop
  ; Re-start
  call i2c_start
  ; Send device address (read)
  lda tslAddress
  or #1
  sta R0
  push #0
  push R0
  call i2c_send_byte
  pop
  pop
  ; Read LSB
  push #0
  push #0 ; 0 = ack
  call i2c_read_byte
  pop
  pop
  sts #2
  ; Read MSB
  push #0
  push #1 ; 1 = nack
  call i2c_read_byte
  pop
  pop
  sts #1
  ; I²C stop
  call i2c_stop
   ; Combine LSB/MSB
  lds #1, R1
  lds #2
  lsl             ; shift MSB to high 8 bits
  lsl
  lsl
  lsl
  lsl
  lsl
  lsl
  lsl
  or R1           ; combine with LSB
  sts #5
  ; free local variables
  pop
  pop
  ; return
  ret  

write8:   ; void write8(uint8_t reg, uint8_t value)
  ; I²C start
  call i2c_start
  ; Send device address (write)
  push #0
  push tslAddress
  call i2c_send_byte
  pop
  pop
  ; Send register address
  lds #3, R0
  push #0
  push R0
  call i2c_send_byte
  pop
  pop
  ; Send value
  lds #2, R0
  push #0
  push R0
  call i2c_send_byte
  pop
  pop
  ; I²C stop
  call i2c_stop
  ret
