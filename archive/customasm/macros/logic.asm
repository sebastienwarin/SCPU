#ruledef macros_logic
{
    not => asm
    {
      nor #0
    }
    not {operand: value_or_address} => asm
    {
      lda {operand}
      not
    }

    and {operand: value_or_address} => asm
    {
      not
      sta RPAR
      lda {operand}
      not
      nor RPAR
    } 

    nand {operand: value_or_address} => asm
    {
      and {operand}
      not
    } 

    or {operand: value_or_address} => asm
    {
      nor {operand}
      not
    }

    xor {operand: value_or_address} => asm
    {
      sta RPAR+1
      nand {operand}
      sta RPAR+2      ; NOT (A AND B)
      lda RPAR+1
      or {operand}    ; (A OR B)
      and RPAR+2      ; (A OR B) AND (NOT (A AND B))
    }

    lsl => asm
    {
      sta RPAR
      add RPAR
    }
    lsl {operand: value_or_address} => asm
    {
      lda {operand}
      add {operand}
    }

    rol => asm
    {
      lsl
      adc #0
    }
    rol {operand: value_or_address} => asm
    {
      lda {operand}
      rol
    }

    ror => asm
    {
      rol
      rol
      rol
      rol
      rol
      rol
      rol
      rol
      rol
      rol
      rol
      rol
      rol
      rol
      rol
    }
    ror {operand: value_or_address} => asm
    {
      lda {operand}
      ror
    }

    lsr => asm
    {
      ror
      and MASK_LSR
    }
    lsr {operand: value_or_address} => asm
    {
      lda {operand}
      lsr
    }
}

#bank consts
MASK_LSR: #d16 0x7FFF