// -----------------------------------------------------------------------------
// scpu_core.v
// S-CPU core (2-phase S0/S1) adapted for Gowin FPGA memories:
//   - Program ROM  : Gowin_DPB (dual-port, synchronous read, 1-cycle latency)
//   - Data RAM     : Gowin_SP  (single-port, synchronous write/read)
//
// Instruction format:
//   IR[15:14] = opcode
//   Addressing mode:
//     IR[13] == 0        => MODE_ROM (IR[12:0] = ROM lookup)
//     IR[13:11] == 110   => MODE_IMM (imm 11-bit, IR[10:0])
//     IR[13:11] == 100   => MODE_RAM (addr 11-bit, IR[10:0])
//     IR[13:11] == 101   => MODE_IO
//     IR[13:11] == 111   => MODE_INDR (indirection)
//
// Timing with 1-cycle-latency memories:
//   - S0: present addresses (ROM.A = PC, and prepare ROM.B / RAM / IO)
//   - S1: consume data (IR <= ROM.A; B_val <= source) and execute, then back to S0.
// -----------------------------------------------------------------------------

module scpu_core #(
    parameter integer ROM_AW       = 16,   // 64K address space
    parameter integer RAM_AW       = 11,   // 2K RAM
    parameter [2:0]  INTERNAL_DEV  = 3'd7  // IO: reg 0xF, bit0=1 -> set CARRY
)(
    input  wire        ce,    // clock enable for core (can be 1'b1 for full speed)
    input  wire        clk,
    input  wire        reset,

    // Generic IO bus
    output wire        io_rd,
    output wire        io_wr,        // 1-cycle strobe in S1 on STA/IO
    output wire [2:0]  io_dev,       // IR[10:8]
    output wire [7:0]  io_reg,       // IR[7:0]
    output wire [15:0] io_wdata,     // ACC
    input  wire [15:0] io_rdata
);
    // ---- Opcodes ----
    localparam [1:0] OP_NOR = 2'b00;
    localparam [1:0] OP_ADD = 2'b01;
    localparam [1:0] OP_STA = 2'b10;
    localparam [1:0] OP_JCC = 2'b11;

    // ---- Addressing modes ----
    localparam [2:0] MODE_ROM  = 3'b000;   // if IR[13]==0
    localparam [2:0] MODE_RAM  = 3'b100;   // IR[13:11]==100
    localparam [2:0] MODE_IO   = 3'b101;   // IR[13:11]==101
    localparam [2:0] MODE_IMM  = 3'b110;   // IR[13:11]==110
    localparam [2:0] MODE_INDR = 3'b111;   // IR[13:11]==111

    // ---- Machine registers ----
    reg  [15:0] PC, IR, ACC;
    reg         CARRY, INDIRECTED, STEP; // STEP: 0=S0, 1=S1

    // ---- Decode ----
    wire [1:0]  opcode    = IR[15:14];
    wire        bit13     = IR[13];
    wire [2:0]  am_full   = IR[13:11];
    wire [10:0] operand   = IR[10:0];
    wire [2:0]  addr_mode = (bit13==1'b0) ? MODE_ROM : am_full;

    // Mode helper fields
    wire [12:0] rom_addr13 = IR[12:0];                 // MODE_ROM
    wire [10:0] ram_addr11 = IR[10:0];                 // MODE_RAM/INDR
    wire [15:0] imm11_w    = {5'b0, IR[10:0]};         // MODE_IMM

    // ---- ROM DPB (dual-port) ----
    wire [15:0] rom_dout_A, rom_dout_B;
    reg  [15:0] rom_addr_A, rom_addr_B;

    Gowin_DPB U_ROM (
        .douta(rom_dout_A), .clka(clk), .ocea(1'b1), .cea(1'b1), .ada(rom_addr_A),
        .doutb(rom_dout_B), .clkb(clk), .oceb(1'b1), .ceb(1'b1), .adb(rom_addr_B)
    );

    // ---- RAM SP ----
    reg  [RAM_AW-1:0] ram_addr_r;
    reg  [15:0]       ram_din_r;
    wire [15:0]       ram_dout_w;
    reg               ram_we_r;

    Gowin_SP U_RAM (
        .dout (ram_dout_w),
        .clk  (clk),
        .oce  (1'b1),
        .ce   (1'b1),
        .reset(reset),
        .wre  (ram_we_r),
        .ad   (ram_addr_r),
        .din  (ram_din_r)
    );

    // ---- IO strobes + latched fields ----
    reg        io_wr_r, io_rd_r;
    reg [2:0]  io_dev_r;
    reg [7:0]  io_reg_r;
    reg [15:0] io_wdata_r;
    wire [2:0]  cur_io_dev_w = IR[10:8];
    wire [7:0]  cur_io_reg_w = IR[7:0];

    assign io_wr    = io_wr_r;
    assign io_rd    = io_rd_r;
    assign io_dev   = io_dev_r;
    assign io_reg   = io_reg_r;
    assign io_wdata = io_wdata_r;

    // ---- Source B in S1 ----
    wire [15:0] B_val_w =
        (addr_mode==MODE_ROM)                        ? rom_dout_B :   // lookup ROM @ IR[12:0]
        (addr_mode==MODE_IMM)                        ? imm11_w    :   // immediate 11b
        ((addr_mode==MODE_RAM) || (addr_mode==MODE_INDR)) ? ram_dout_w : // RAM
        (addr_mode==MODE_IO)                         ? io_rdata   :   // IO
                                                        16'h0000;

    // STA to internal carry control
    wire hit_internal_carry_w =
        (addr_mode==MODE_IO) && (cur_io_dev_w==INTERNAL_DEV) && (cur_io_reg_w==8'h0F);

    // Branch bookkeeping
    reg flush_r;        // 1 => do a "flush" cycle after a taken branch
    wire take_branch_w = (opcode==OP_JCC) && (!CARRY);

    // =========================================================================
    // FSM S0 / S1
    // =========================================================================
    always @(posedge clk) begin
        if (reset) begin
            PC <= 16'h0000; IR <= 16'h0000; ACC <= 16'h0000;
            CARRY <= 1'b0; INDIRECTED <= 1'b0; STEP <= 1'b0;
            ram_we_r <= 1'b0; ram_din_r <= 16'h0000;
            io_wr_r  <= 1'b0; io_rd_r   <= 1'b0;
            rom_addr_A <= 16'h0000; rom_addr_B <= 16'h0000;
            ram_addr_r <= {RAM_AW{1'b0}};
            flush_r    <= 1'b0;
        end else if (ce) begin
            // defaults each cycle
            ram_we_r <= 1'b0;
            io_wr_r  <= 1'b0;
            io_rd_r  <= 1'b0;

            if (STEP==1'b0) begin
                // ---------------- S0 : present addresses ----------------
                // Instruction fetch (ROM port A) if not being rewritten via INDR
                if (!INDIRECTED) begin
                    rom_addr_A <= PC;
                    PC <= PC + 16'd1;
                end

                // Prepare B source for S1
                case (addr_mode)
                    MODE_ROM: begin
                        // Lookup in ROM @ IR[12:0] via port B
                        rom_addr_B <= { {(ROM_AW-13){1'b0}}, rom_addr13 };
                    end
                    MODE_IMM: begin
                        // immediate: nothing to place
                    end
                    MODE_RAM, MODE_INDR: begin
                        ram_addr_r <= ram_addr11[RAM_AW-1:0];
                    end
                    MODE_IO: begin
                        io_rd_r <= 1'b1; // optional read strobe for devices
                    end
                    default: ;
                endcase

                STEP <= 1'b1; // -> S1

            end else begin
                // ---------------- S1 : consume / execute ----------------

                // 1) Flush cycle after a taken branch:
                //    Only latch the target instruction and do nothing else.
                if (flush_r) begin
                    IR     <= rom_dout_A; // target instruction (ROM.A placed in S0)
                    flush_r <= 1'b0;
                end
                // 2) Indirection has absolute priority
                else if (addr_mode==MODE_INDR) begin
                    // Keep the current opcode; replace [13:0] with the fetched word
                    IR         <= { IR[15:14], B_val_w[13:0] };
                    INDIRECTED <= 1'b1;

                    // We pre-incremented PC in S0; since we skip the next fetch in
                    // the following S0 (INDIRECTED==1), "give back" one PC:
                    PC <= PC - 16'd1;

                end else begin
                    case (opcode)
                        OP_NOR: begin
                            ACC <= ~(ACC | B_val_w);
                        end
                        OP_ADD: begin
                            {CARRY, ACC} <= {1'b0, ACC} + {1'b0, B_val_w};
                        end
                        OP_STA: begin
                            if (hit_internal_carry_w) begin
                                CARRY <= 1'b1;
                            end else if (addr_mode==MODE_RAM) begin
                                ram_din_r <= ACC;
                                ram_we_r  <= 1'b1;  // sync RAM write
                            end else if (addr_mode==MODE_IO) begin
                                // Capture executed instruction fields (this S1)
                                io_dev_r   <= IR[10:8];
                                io_reg_r   <= IR[7:0];
                                io_wdata_r <= ACC;
                                io_wr_r    <= 1'b1; // 1-cycle pulse
                            end
                        end
                        OP_JCC: begin
                            if (take_branch_w) begin
                                PC     <= B_val_w;
                                flush_r <= 1'b1; // arm flush to latch target instruction
                            end
                            // Clear carry *after* execution, per S-CPU semantics
                            CARRY <= 1'b0;
                        end
                        default: ; // NOP
                    endcase

                    // Latch the fall-through instruction only if no branch taken
                    // and we're not doing INDR
                    if (!take_branch_w) begin
                        IR <= rom_dout_A; // next sequential instruction
                    end
                end

                INDIRECTED <= 1'b0; // back to 0 for the next cycle
                STEP <= 1'b0;       // -> S0
            end
        end
    end

endmodule