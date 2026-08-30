#ruledef macro_stack
{
    lds {index: immvalue} => asm
    {
      lda SP
      add {index}
      sta RPEEK
      lda @(RPEEK)
    }
    lds {index: immvalue}, {address: address} => asm
    {
      lds {index}
      sta {address}
    }
    sts {index: immvalue} => asm
    {
      sta RPAR
      lda SP
      add {index}
      sta RPEEK
      lda RPAR
      sta @(RPEEK)
    }
    sts {index: immvalue}, {operand: value_or_address} => asm
    {
      lda SP
      add {index}
      sta RPEEK
      lda {operand}
      sta @(RPEEK)
    }

    pop => asm
    {
      inc SP
      sta SP
      lda @SP
    }
    pop {address: address} => asm
    {
      pop
      sta {address}
    }
    
    push => asm
    {
      sta @SP
      dec SP
      sta SP
    }
    push {operand: value_or_address} => asm
    {
      lda {operand}
      push
    }

    call {address: jump_address} =>
    {
      ; Fixed cost after the CALL site (1 clear, 5 push, 2 jump)
      base_address = $ + 8

      ; Pass 1 : cost if we emit NOTHING yet
      p1_page = ((base_address >> 11) > 0 ? 1 : 0)      ; need page-ADD ?
      p1_low  = ((base_address & 0x7FF) > 0 ? 1 : 0)    ; need low-ADD  ?
      cost1   = p1_page + p1_low                        ; 0, 1 or 2

      ; Pass 2 : cost after adding cost1
      tmp     = base_address + cost1
      p2_page = ((tmp >> 11) > 0 ? 1 : 0)
      p2_low  = ((tmp & 0x7FF) > 0 ? 1 : 0)
      add_count = p2_page + p2_low                      ; converged cost

      ; Final return address
      return_address = base_address + add_count 

      ; Extract ROM's page index and low offset
      page_idx  = return_address >> 11      ; page-index (bits 11-12)
      low       = return_address & 0x7FF    ; low offset (bits 0-10)

      ; Safety guard
      assert(return_address < RAM, "Invalid return address: outside the ROM memory space")

      ; Emit code
      emit_clear_accu() @
      (page_idx > 0 ? emit_add_page_offset(page_idx-1) : nop()) @
      (low > 0 ? emit_add_immediate(low) : nop()) @
      emit_push_and_jump(address)
    }

    ret => asm
    {
      pop RRET
      jmp @(RRET)
    }
}

#bank consts
ROM_PAGES: #d16 0x0800, 0x1000, 0x1800   ;  page 1, page 2, page 3

; Functions for `call` macro
#fn nop() => asm { }
#fn emit_clear_accu()          => asm { clr }
#fn emit_add_page_offset(idx)  => asm { add ROM_PAGES+{idx} }
#fn emit_add_immediate(value)    => asm { add #({value}) }
#fn emit_push_and_jump(address)   => asm
{
  push
  jmp @{address}
}