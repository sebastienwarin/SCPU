`ifndef SCPU_DEFS_VH
`define SCPU_DEFS_VH

// ---- Opcodes ----
`define OP_NOR 2'b00
`define OP_ADD 2'b01
`define OP_STA 2'b10
`define OP_JCC 2'b11

// ---- Addressing modes ----
`define MODE_ROM  3'b000 // implied when IR[13]==0
`define MODE_RAM  3'b100
`define MODE_IO   3'b101
`define MODE_IMM  3'b110
`define MODE_INDR 3'b111

// ---- Device IDs ----
`define DEV_DEMO      3'd0
`define DEV_INTERNAL  3'd7

`endif // SCPU_DEFS_VH