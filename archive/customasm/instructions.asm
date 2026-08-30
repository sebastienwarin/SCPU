#subruledef address
{
  {absolute_addr: u14} => absolute_addr
  @{indirect_addr: u14} => 0b111 @ indirect_addr`11
}

#subruledef immvalue
{
  #{value: u11} => 0b110 @ value
}

#subruledef value_or_address
{
  {address: address} => address
  {value: immvalue} => value
}

#subruledef jump_address
{
  {address: u11} => 0b110 @ address
  @{address: u14} => address
}

; OO XX AAAA AAAA AAAA
#ruledef instructions
{
    nor {operand: value_or_address} => 0b00 @ operand
    add {operand: value_or_address} => 0b01 @ operand
    sta {address: address} => 0b10 @ address
    jcc {address: jump_address} => 0b11 @ address
}