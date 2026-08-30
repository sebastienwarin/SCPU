[macro nop]
sta RSINK

[macro clr]
nor MAX_VALUE

[macro lda {operand}]
clr
add {operand}

[macro mov {dest}, {src}]
lda {src}
sta {dest}

[macro jz {address}]
add MAX_VALUE
jcc {address}

[macro jnz {address}]
add MAX_VALUE
jcs {address}

[macro jcs {address}]
jcc $+2
jcc {address}

[macro jmp {address}]
jcc {address}
jcc {address}

[macro halt]
jmp $

[macro rst]
jmp 0x0
