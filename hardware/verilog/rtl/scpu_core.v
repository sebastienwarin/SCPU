//------------------------------------------------------------------------------
// S-CPU core (2-stage S0/S1) - instruction set per project spec
// Opcodes: NOR, ADD, STA, JCC
// Addressing modes: ROM (implicit when IR[13]==0), RAM, IO, IMM, INDR
// Indirection: IR ← { (INDR ? IR[15:14] : data_bus[15:14]), data_bus[13:0] }
//------------------------------------------------------------------------------
`timescale 1ns/1ps
`include "rtl/include/scpu_defs.vh"

module scpu_core #(
    parameter integer ROM_AW       = 16,
    parameter integer RAM_AW       = 11
)(
    input  wire        clk,
    input  wire        reset,

    // Generic IO bus
    output wire        io_rd,
    output wire        io_wr,      // 1-cycle strobe on STA/IO
    output wire [2:0]  io_dev,
    output wire [3:0]  io_reg,
    output wire [15:0] io_wdata,
    input  wire [15:0] io_rdata
);
    // ---- Machine registers ----
    reg  [15:0] PC, IR, ACC;
    reg         CARRY, INDIRECTED, STEP; // STEP: 0=S0, 1=S1

    // ---- Decode helpers ----
    wire [1:0]  opcode   = IR[15:14];
    wire        bit13    = IR[13];
    wire [2:0]  am_full  = IR[13:11];
    wire [10:0] operand  = IR[10:0];
    wire [11:0] romfield = IR[11:0];

    wire [2:0] addr_mode = (bit13==1'b0) ? `MODE_ROM : am_full;

    // One-cycle indirection happens in S1
    wire doing_indirection = (STEP==1'b1) && (addr_mode==`MODE_INDR);

    // ROM address: S0 → PC ; S1 → zero-extend(IR[11:0])
    wire [ROM_AW-1:0] rom_addr = (STEP==1'b0) ? PC
                          : { {(ROM_AW-12){1'b0}}, romfield };

    wire [15:0] rom_dout;
    rom #(.AW(ROM_AW)) u_rom (.addr(rom_addr), .dout(rom_dout));

    wire [RAM_AW-1:0] ram_addr = operand[RAM_AW-1:0];
    reg               ram_we;
    reg  [15:0]       ram_din;
    wire [15:0]       ram_dout;
    ram #(.AW(RAM_AW)) u_ram (
        .clk (clk), .we (ram_we), .addr (ram_addr), .din (ram_din), .dout (ram_dout)
    );

    // IO map
    assign io_dev   = IR[10:8];
    assign io_reg   = IR[3:0];
    assign io_wdata = ACC;

    // Fetch control
    wire should_fetch_ir = (STEP==1'b0) && !INDIRECTED;
    wire isROMEnable = (addr_mode==`MODE_ROM) || should_fetch_ir;
    wire isRAMEnable = ((addr_mode==`MODE_RAM) || (addr_mode==`MODE_INDR)) && !should_fetch_ir;
    wire isIOEnable  = (STEP!=1'b0) && (addr_mode==`MODE_IO);

    // Data bus priority: ROM > RAM > IO
    wire [15:0] data_bus = isROMEnable ? rom_dout
                         : isRAMEnable ? ram_dout
                         : isIOEnable  ? io_rdata
                         : 16'h0000;

    // Immediate (IMM)
    wire [15:0] imm_val = {5'b0, operand};
    wire [15:0] B_val   = (addr_mode==`MODE_IMM) ? imm_val : data_bus;

    // ALU (ADD/NOR)
    wire [16:0] add_ext = {1'b0, ACC} + {1'b0, B_val};
    wire [15:0] add_R   = add_ext[15:0];
    wire        add_C   = add_ext[16];
    wire [15:0] nor_R   = ~(ACC | B_val);

    // IO strobes
    reg io_wr_r;
    assign io_rd = isIOEnable;
    assign io_wr = io_wr_r;

    wire hit_internal_carry = isIOEnable && (io_dev==`DEV_INTERNAL) && (io_reg==4'hF);

    // 2:1 mux for INDR MSBs (behavioral 74LS157)
    wire [1:0] msb_mux_A = data_bus[15:14];
    wire [1:0] msb_mux_B = IR[15:14];
    wire [1:0] msb_mux_Y = doing_indirection ? msb_mux_B : msb_mux_A;

    // FSM S0/S1
    always @(posedge clk or posedge reset) begin
        if (reset) begin
            PC <= 16'h0000; IR <= 16'h0000; ACC <= 16'h0000;
            CARRY <= 1'b0; INDIRECTED <= 1'b0; STEP <= 1'b0;
            ram_we <= 1'b0; ram_din <= 16'h0000; io_wr_r <= 1'b0;
        end else begin
            ram_we  <= 1'b0;
            io_wr_r <= 1'b0;

            if (STEP==1'b0) begin
                // S0
                if (opcode==`OP_JCC) CARRY <= 1'b0;  // clear carry at JCC start

                if (should_fetch_ir) begin
                    IR <= rom_dout;   // fetch
                    PC <= PC + 16'd1;
                end

                STEP <= 1'b1; // → S1
            end else begin
                // S1
                if (INDIRECTED) INDIRECTED <= 1'b0;

                if (addr_mode==`MODE_INDR) begin
                    // IR ← { (INDR ? IR[15:14] : data_bus[15:14]), data_bus[13:0] }
                    IR         <= { msb_mux_Y, data_bus[13:0] };
                    INDIRECTED <= 1'b1;
                end else begin
                    case (opcode)
                        `OP_NOR: begin
                            ACC <= nor_R; // CARRY unchanged
                        end
                        `OP_ADD: begin
                            ACC   <= add_R;
                            CARRY <= add_C;
                        end
                        `OP_STA: begin
                            if (hit_internal_carry) begin
                                CARRY <= 1'b1;
                            end else if (isRAMEnable) begin
                                ram_din <= ACC; ram_we <= 1'b1;
                            end else if (isIOEnable) begin
                                io_wr_r <= 1'b1; // external IO write
                            end
                        end
                        `OP_JCC: begin
                            if (!CARRY) begin
                                PC <= B_val; // IMM or data_bus
                            end
                        end
                        default: /* NOP */ ;
                    endcase
                end

                STEP <= 1'b0; // → S0
            end
        end
    end
endmodule
