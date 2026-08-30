`timescale 1ns/1ps
`include "rtl/include/scpu_defs.vh"

module scpu_tb;
    // Clock & reset
    reg clk   = 1'b0;
    reg reset = 1'b1;

    // Core IO bus wires
    wire        io_rd, io_wr;
    wire [2:0]  io_dev;
    wire [3:0]  io_reg;
    wire [15:0] io_wdata;
    wire [15:0] io_rdata_core;

    // DUT
    scpu_core #(
        .ROM_AW(16),
        .RAM_AW(11)
    ) u_dut (
        .clk(clk), .reset(reset),
        .io_rd(io_rd), .io_wr(io_wr), .io_dev(io_dev), .io_reg(io_reg),
        .io_wdata(io_wdata), .io_rdata(io_rdata_core)
    );

    // Device #0: LED/7-seg
    wire [3:0]  led4;
    wire [7:0]  seg;
    wire [3:0]  dig;
    wire [15:0] io_rdata_led;

    mmio_device u_dev0 (
        .clk(clk), .reset(reset),
        .io_rd(io_rd), .io_wr(io_wr), .io_dev(io_dev), .io_reg(io_reg),
        .io_wdata(io_wdata), .io_rdata(io_rdata_led),
        .led4(led4), .seg(seg), .dig(dig)
    );

    // IO read mux (add more devices here)
    assign io_rdata_core = (io_rd && (io_dev==`DEV_DEMO)) ? io_rdata_led
                                                             : 16'h0000;

    // 100 MHz clock (10 ns period → toggle every 5 ns)
    always #5 clk = ~clk;

    // -------------------------
    // HALT detection: JCC IMM to current PC
    // -------------------------
    // hierarchical taps (acceptable in TB)
    wire [15:0] IR   = u_dut.IR;
    wire [15:0] PC   = u_dut.PC;
    wire        STEP = u_dut.STEP;   // 0=S0, 1=S1
    wire        CARRY= u_dut.CARRY;

    wire [1:0] opcode    = IR[15:14];
    wire [10:0] operand  = IR[10:0];
    wire [2:0] mode_bits = (IR[13]==1'b0) ? `MODE_ROM : IR[13:11];
    wire [15:0] target_imm = {5'b0, operand};

    wire halt_jcc_self = (STEP==1'b1) && (opcode==`OP_JCC) && (!CARRY)
                       && (mode_bits==`MODE_IMM) && (target_imm == (PC - 1));

    integer cycles;

    initial begin
        $dumpfile("scpu.vcd");
        $dumpvars(0, scpu_tb);

        cycles = 0;

        // Release reset
        repeat (5) @(posedge clk);
        reset = 1'b0;

        // Monitor loop
        forever begin
            @(posedge clk);
            if (!reset) begin
                cycles = cycles + 1;

                if (halt_jcc_self) begin
                    $display("[%0t] HALT detected (JCC IMM to PC=%0h).", $time, PC);
                    $finish;
                end

                if (cycles == 1_000_000) begin
                    $display("[%0t] Timeout reached.", $time);
                    $finish;
                end
            end
        end
    end
endmodule