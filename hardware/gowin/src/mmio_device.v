// -----------------------------------------------------------------------------
// mmio_device.v
// MMIO Device #0: 4 LEDs + dual 7-segment (active-LOW segments).
//
// Register map (dev = 0):
//   0x01 (W/R): disp_value[7:0]  -> drives the dual 7-seg (hex, [7:4]=tens, [3:0]=ones)
//   0x02 (W/R): leds[3:0]        -> 4 discrete LEDs (active-HIGH internally)
//
// Reads are optional; writes are synchronous.
// -----------------------------------------------------------------------------

module mmio_device (
    input  wire        clk,
    input  wire        reset,

    // Core-side MMIO bus
    input  wire        io_rd,
    input  wire        io_wr,
    input  wire [2:0]  io_dev,   // device id
    input  wire [7:0]  io_reg,   // register index
    input  wire [15:0] io_wdata,
    output wire [15:0] io_rdata,

    // Physical outputs
    output wire [3:0]  led4,     // 4 LEDs (active-HIGH internally)
    output wire [6:0]  seg,      // ABCDEFG active-LOW -> map to o_digitalTube[6:0]
    output wire        sel       // 1 = tens active, 0 = ones active -> map to o_sel
);
    localparam [2:0] DEV_ID = 3'd0;

    reg [7:0] disp_value_r;
    reg [3:0] leds_r;

    // Writes
    always @(posedge clk or posedge reset) begin
        if (reset) begin
            disp_value_r <= 8'h00;
            leds_r       <= 4'b0000;
        end else if (io_wr && (io_dev == DEV_ID)) begin
            case (io_reg)
                8'h01: disp_value_r <= io_wdata[7:0]; // 0x2801
                8'h02: leds_r       <= io_wdata[3:0]; // 0x2802
                default: ;
            endcase
        end
    end

    // Optional reads
    assign io_rdata =
        (io_rd && io_dev==DEV_ID) ? (
            (io_reg==8'h01) ? {8'h00, disp_value_r} :
            (io_reg==8'h02) ? {12'h000, leds_r}     :
                              16'h0000
        ) : 16'h0000;

    assign led4 = leds_r;

    // Dual 7-seg (active-LOW), ~200 Hz/digit by default
    wire [6:0] seg_w;
    wire       sel_w;  // 0=ones, 1=tens

    seg7_dual_hex u_seg (
        .clk  (clk),
        .reset(reset),
        .val  (disp_value_r), // [7:4]=tens, [3:0]=ones
        .seg  (seg_w),        // active-LOW
        .sel  (sel_w)         // 1=tens, 0=ones
    );

    assign seg = seg_w;
    assign sel = sel_w; // matches Gowin sample: o_sel=1 => tens active

endmodule