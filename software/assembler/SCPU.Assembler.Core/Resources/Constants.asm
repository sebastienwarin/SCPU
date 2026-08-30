#const RAM = 0x12000

#const ZEROPAGE = RAM
#const USERPAGE = RAM + 0x100
#const RESVPAGE = RAM + 0x700

#const R0 = RESVPAGE + 0x00
#const R1 = RESVPAGE + 0x01
#const R2 = RESVPAGE + 0x02
#const R3 = RESVPAGE + 0x03
#const R4 = RESVPAGE + 0x04
#const R5 = RESVPAGE + 0x05
#const R6 = RESVPAGE + 0x06
#const R7 = RESVPAGE + 0x07
#const R8 = RESVPAGE + 0x08
#const R9 = RESVPAGE + 0x09
#const RPAR = RESVPAGE + 0x0A
#const RRET = RESVPAGE + 0x0B
#const RPEEK = RESVPAGE + 0x0C
#const FP = RESVPAGE + 0x0E
#const SP = RESVPAGE + 0x0F
#const TEMPVAR = RESVPAGE + 0x10

#const IODEV = RAM + 0x800
#const SCPUIO = RAM + 0xF00
#const CF = SCPUIO + 0x0F
#const RSINK = SCPUIO + 0x10