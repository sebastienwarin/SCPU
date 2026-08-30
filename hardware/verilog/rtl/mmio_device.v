//------------------------------------------------------------------------------
// MMIO Device #0: 4 LEDs + 4-digit 7-segment display (hex)
// Register map (dev = `DEV_DEMO`):
//   reg 0x1 (W/R) : disp_value[15:0] → 4 nibbles shown on 7-seg
//   reg 0x2 (W/R) : led[3:0]         → 4 discrete LEDs
// Reads are optional; writes are synchronous to clk.
//------------------------------------------------------------------------------
`timescale 1ns/1ps
`include "rtl/include/scpu_defs.vh"

module mmio_device (
    input  wire        clk,
    input  wire        reset,

    // Core-side MMIO bus
    input  wire        io_rd,
    input  wire        io_wr,
    input  wire [2:0]  io_dev,
    input  wire [3:0]  io_reg,
    input  wire [15:0] io_wdata,
    output wire [15:0] io_rdata,

    // Physical outputs
    output wire [3:0]  led4,
    output wire [7:0]  seg,    // segments a..g,dp
    output wire [3:0]  dig     // digit selects (4 digits)
);
    localparam [2:0] DEV_ID = `DEV_DEMO;

    reg [15:0] disp_value;
    reg [3:0]  leds;

    // Synchronous writes
    always @(posedge clk or posedge reset) begin
        if (reset) begin
            disp_value <= 16'h0000;
            leds       <= 4'b0000;
        end else if (io_wr && (io_dev == DEV_ID)) begin
            case (io_reg)
                4'h1: disp_value <= io_wdata;           // reg 0x1 : 7-seg
                4'h2: leds       <= io_wdata[3:0];      // reg 0x2 : LEDs
                default: /* no-op */ ;
            endcase
        end
    end

    // Optional reads (combinational)
    assign io_rdata = (io_rd && (io_dev == DEV_ID))
                    ? ((io_reg==4'h1) ? disp_value
                      : (io_reg==4'h2) ? {12'h000, leds}
                      : 16'h0000)
                    : 16'h0000;

    assign led4 = leds;

    // 7-segment 4-digit scanner (hex)
    sevenseg_scan #(
        .COMMON_ANODE(1)
    ) u_7s (
        .clk   (clk),
        .reset (reset),
        .value (disp_value),
        .seg   (seg),
        .dig   (dig)
    );
endmodule
