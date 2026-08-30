//------------------------------------------------------------------------------
// 4-digit 7-segment multiplexed scanner
// value[15:12]=digit3 ... value[3:0]=digit0
// COMMON_ANODE=1 → active-low seg & digit lines
// COMMON_ANODE=0 → active-high seg & digit lines
// Refresh target ≈ 1 kHz per digit (adjust divider to your FPGA clock)
//------------------------------------------------------------------------------
`timescale 1ns/1ps

module sevenseg_scan #(
    parameter integer COMMON_ANODE = 1
)(
    input  wire        clk,
    input  wire        reset,
    input  wire [15:0] value,
    output wire [7:0]  seg,   // a b c d e f g dp
    output wire [3:0]  dig    // digit selects
);
    // Simple clock divider → tick for digit advance
    reg [15:0] div;
    always @(posedge clk or posedge reset) begin
        if (reset)      div <= 16'd0;
        else            div <= div + 16'd1;
    end

    // Digit selector (round-robin)
    reg [1:0] sel;
    always @(posedge clk or posedge reset) begin
        if (reset)      sel <= 2'd0;
        else if (div==16'd0) sel <= sel + 2'd1;
    end

    // Current nibble
    wire [3:0] nibble = (sel==2'd0) ? value[3:0]
                       : (sel==2'd1) ? value[7:4]
                       : (sel==2'd2) ? value[11:8]
                       :                value[15:12];

    // Hex to segments (abcdefg, dp=off)
    function [7:0] hex7seg;
        input [3:0] n;
        begin
            case (n)
                4'h0: hex7seg = 8'b01111110;
                4'h1: hex7seg = 8'b00001100;
                4'h2: hex7seg = 8'b10110110;
                4'h3: hex7seg = 8'b10011110;
                4'h4: hex7seg = 8'b11001100;
                4'h5: hex7seg = 8'b11011010;
                4'h6: hex7seg = 8'b11111010;
                4'h7: hex7seg = 8'b00001110;
                4'h8: hex7seg = 8'b11111110;
                4'h9: hex7seg = 8'b11011110;
                4'hA: hex7seg = 8'b11101110;
                4'hB: hex7seg = 8'b11111000;
                4'hC: hex7seg = 8'b01110010;
                4'hD: hex7seg = 8'b10111100;
                4'hE: hex7seg = 8'b11110010;
                4'hF: hex7seg = 8'b11100010;
                default: hex7seg = 8'b00000010;
            endcase
        end
    endfunction

    wire [7:0] seg_raw = hex7seg(nibble);

    // Active level adaptation
    assign seg = (COMMON_ANODE ? ~seg_raw : seg_raw);

    reg [3:0] dig_onehot;
    always @* begin
        case (sel)
            2'd0: dig_onehot = 4'b0001;
            2'd1: dig_onehot = 4'b0010;
            2'd2: dig_onehot = 4'b0100;
            default: dig_onehot = 4'b1000;
        endcase
    end

    assign dig = (COMMON_ANODE ? ~dig_onehot : dig_onehot);
endmodule