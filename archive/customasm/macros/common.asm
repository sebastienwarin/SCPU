#ruledef macros
{
    nop => asm
    {
      add #0
    }
    clr => asm
    {
      nor MAX_VALUE
    }
    lda {operand: value_or_address} => asm
    {
      clr
      add {operand}
    }
    mov {dest: address}, {source: value_or_address} => asm
    {
      lda {source}
      sta {dest}
    }

    jz {address: jump_address} => asm
    {
      add MAX_VALUE
      jcc {address}
    }
    jnz {address: jump_address} => asm
    {
      add MAX_VALUE
      jcs {address}
    }
    jcs {address: jump_address} => asm
    {
      jcc $+2
      jcc {address}
    }
    jmp {address: jump_address} => asm
    {
      jcc {address}
      jcc {address}
    }

    halt => asm
    {
      jmp $
    }
    rst => asm
    {
      jmp 0x0
    }
}
