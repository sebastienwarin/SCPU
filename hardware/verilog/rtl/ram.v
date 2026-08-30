//------------------------------------------------------------------------------
// Simple 2Kx16 RAM (async read, sync write) - AW parameterizable
// NOTE: For FPGA inference, you may prefer sync read depending on vendor.
//------------------------------------------------------------------------------
`timescale 1ns/1ps

module ram #(
    parameter integer AW = 11  // 2^11 = 2048
)(
    input  wire             clk,
    input  wire             we,
    input  wire [AW-1:0]    addr,
    input  wire [15:0]      din,
    output wire [15:0]      dout
);
    reg [15:0] mem [0:(1<<AW)-1];

    always @(posedge clk) begin
        if (we) mem[addr] <= din;
    end

    assign dout = mem[addr];
endmodule