#ruledef macros_math
{
    inc => asm
    {
      add #1
    }
    inc {operand: value_or_address} => asm
    {
      lda {operand}
      inc
    }
    
    dec => asm
    {
      add MAX_VALUE
    }
    dec {operand: value_or_address} => asm
    {
      lda {operand}
      dec
    }
    
    neg => asm
    {
      not
      add #1
    }
    neg {operand: value_or_address} => asm
    {
      lda {operand}
      neg
    }

    sub {operand: value_or_address} => asm
    {
      not
      add {operand}
      not
    }

    adc {operand: value_or_address} => asm
    {
      jcc $+2       ; if carry not set, jump to last line (ADD) 
      add #1
      add {operand}
    }
    sbc {operand: value_or_address} => asm
    {
      jcc $+2       ; if carry not set, jump to last line (SUB) 
      add #1
      sub {operand}
    }
    ldc {operand: value_or_address} => asm
    {
      jcs $+4
      lda {operand}
      jcc $+6
      lda {operand}
      sec
    }
    
    clc => asm
    {
      jcc $+1
    }
    sec => asm
    {
      mov CF, #1
    }
}