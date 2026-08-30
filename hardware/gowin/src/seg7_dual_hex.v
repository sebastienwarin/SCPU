// -----------------------------------------------------------------------------
// seg7_dual_hex.v
// 2-digit, 7-segment (no decimal point), ACTIVE-LOW segments (ABCDEFG).
// Displays val[7:4] on "tens" and val[3:0] on "ones".
// Scan frequency ~200 Hz/digit for clk ≈ 27 MHz when SCAN_BIT=15.
// -----------------------------------------------------------------------------

module seg7_dual_hex #(
    parameter integer SCAN_BIT = 15  // 27e6 / 2^(15+1) ≈ 412 Hz total => ~206 Hz/digit
)(
    input  wire       clk,
    input  wire       reset,
    input  wire [7:0] val,      // [7:4] = tens, [3:0] = ones
    output wire [6:0] seg,      // ABCDEFG, ACTIVE-LOW (0 = segment ON)
    output wire       sel       // 0 = ones (val[3:0]), 1 = tens (val[7:4])
);
    reg [SCAN_BIT:0] cnt_r;
    always @(posedge clk or posedge reset) begin
        if (reset) cnt_r <= { (SCAN_BIT+1){1'b0} };
        else       cnt_r <= cnt_r + 1'b1;
    end

    // Digit select
    assign sel = cnt_r[SCAN_BIT];

    // Nibble select
    wire [3:0] nibble_w = sel ? val[7:4] : val[3:0];

    // Hex to segments (ACTIVE-LOW ABCDEFG)
    reg [6:0] seg_r;
    always @* begin
        case (nibble_w)
            4'h0: seg_r = 7'b0000001;
            4'h1: seg_r = 7'b1111001;
            4'h2: seg_r = 7'b0010010;
            4'h3: seg_r = 7'b0110000;
            4'h4: seg_r = 7'b1101000;
            4'h5: seg_r = 7'b0100100;
            4'h6: seg_r = 7'b0000100;
            4'h7: seg_r = 7'b1110001;
            4'h8: seg_r = 7'b0000000;
            4'h9: seg_r = 7'b0100000;
            4'hA: seg_r = 7'b0001000;
            4'hB: seg_r = 7'b0000100;
            4'hC: seg_r = 7'b1000110;
            4'hD: seg_r = 7'b0010000;
            4'hE: seg_r = 7'b0000110;
            4'hF: seg_r = 7'b0001110;
            default: seg_r = 7'b1111111; // blank
        endcase
    end

    assign seg = seg_r;

endmodule