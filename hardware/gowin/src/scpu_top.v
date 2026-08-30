// -----------------------------------------------------------------------------
// scpu_top.v
// Tang Primer 25K top-level design
// - Clocking: 27 MHz base clock + PLL → 81 MHz (core clock)
// - Speed mode latched at reset (switch[2:1])
// - CE (clock enable) generator: step/auto with selectable speeds
// - Debounce / one-pulse for step mode button (active-LOW PMOD button)
// - LEDs are active-LOW on the board; drive with inverted active-HIGH signals
// - Dual 7-segment display (active-LOW) for hexadecimal output
// -----------------------------------------------------------------------------

module scpu_top (
    input  wire        i_clk,          // 27 MHz base clock
    input  wire        i_rst,          // async reset from dock (active-HIGH)
    input  wire [3:0]  i_button,       // PMOD buttons, active-LOW
    input  wire [3:0]  i_switch,       // PMOD switches, active-LOW
    output wire [7:0]  o_led,          // PMOD LEDs, active-LOW on board
    output wire [6:0]  o_digitalTube,  // 7-seg (ABCDEFG), active-LOW
    output wire        o_sel           // 1 = tens digit, 0 = ones digit
);

    // Base clock (27 MHz on Tang Primer)
    wire clk_27m = i_clk;

    // PLL @81 MHz (Gowin IP configured for 3x)
    wire clk_core;
    Gowin_PLL u_pll (
        .clkin  (clk_27m),
        .clkout0(clk_core)
    );

    // Reset synchronized into core clock domain
    wire rst_sync_w;
    sync_2ff u_rst_core (
      .clk    (clk_core),
      .d_async(i_rst),
      .q      (rst_sync_w)
    );

    // Latch speed selection on reset
    //    i_switch[2:1]: 00=~10 Hz, 01=~2 MHz, 10=~27 MHz, 11=81 MHz
    reg [1:0] speed_mode_r /* synthesis preserve */ = 2'b11; // default = 81 MHz
    always @(posedge clk_core) begin
        if (rst_sync_w)
            speed_mode_r <= ~i_switch[2:1];
    end

    // Synchronize step/auto switch into core domain
    wire mode_step_w;
    sync_2ff u_sync_mode (
        .clk    (clk_core),
        .d_async(i_switch[0]),
        .q      (mode_step_w)
    );

    // One-pulse generator for step mode (core domain)
    // ~50 ms @81 MHz → 81e6 * 0.050 = 4,050,000 cycles
    wire step_pulse_w;
    btn_onepulse #(.DEBOUNCE(4_050_000))
    u_step (
        .i_clk   (clk_core),
        .i_rst   (rst_sync_w),
        .i_btn_n (i_button[0]), // active-LOW button
        .o_pulse (step_pulse_w)
    );

    // -------------------------------------------------------------------------
    // Clock Enable generator (core domain)
    // Modes selected by speed_mode_r (latched at reset):
    //   00: ~10 Hz   (tick every 8,100,000 cycles @81 MHz)
    //   01: ~2  MHz  (NCO exact ratio 2/81)
    //   10: ~27 MHz  (exact divide-by-3)
    //   11: 81 MHz   (full rate, CE=1)
    // -------------------------------------------------------------------------
    reg        ce_auto_r;

    // ~10 Hz counter
    reg [22:0] cnt_10hz;                 // needs to count up to 8,099,999 (< 2^23)
    localparam integer TEN_HZ_TERM = 23'd8_099_999;

    // ~2 MHz NCO (add 2, mod 81)
    reg  [6:0] acc_2m;                   // 0..80 fits in 7 bits
    wire [7:0] sum_2m  = {1'b0, acc_2m} + 8'd2;
    wire       hit_2m  = (sum_2m >= 8'd81);
    wire [6:0] nxt_2m  = hit_2m ? (sum_2m - 8'd81) : sum_2m[6:0];

    // ~27 MHz exact /3 divider
    reg  [1:0] div_27m;                  // 0,1,2 cycle counter

    always @(posedge clk_core) begin
        if (rst_sync_w) begin
            ce_auto_r <= 1'b0;
            cnt_10hz  <= 23'd0;
            acc_2m    <= 7'd0;
            div_27m   <= 2'd0;
        end else if (ce_auto_r && speed_mode_r == 2'b11) begin
            // full-rate: keep CE single-cycle
            ce_auto_r <= 1'b0;
        end else begin
            ce_auto_r <= 1'b0;

            case (speed_mode_r)
                2'b00: begin
                    // ~10 Hz
                    if (cnt_10hz == TEN_HZ_TERM) begin
                        cnt_10hz  <= 23'd0;
                        ce_auto_r <= 1'b1;
                    end else begin
                        cnt_10hz <= cnt_10hz + 1'b1;
                    end
                end

                2'b01: begin
                    // ~2 MHz using NCO (exact average 2/81)
                    acc_2m <= nxt_2m;
                    if (hit_2m) ce_auto_r <= 1'b1;
                end

                2'b10: begin
                    // ~27 MHz exact divide-by-3
                    if (div_27m == 2'd2) begin
                        div_27m   <= 2'd0;
                        ce_auto_r <= 1'b1;
                    end else begin
                        div_27m <= div_27m + 1'b1;
                    end
                end

                default: begin
                    // 81 MHz full rate
                    ce_auto_r <= 1'b1;
                end
            endcase
        end
    end

    // Hold CE active during reset; release on first auto tick
    reg hold_ce;
    always @(posedge clk_core) begin
        if (rst_sync_w) begin
            hold_ce <= 1'b1;
        end else if (hold_ce && ce_auto_r) begin
            hold_ce <= 1'b0;
        end
    end

    // Global CE (step vs auto)
    wire ce_w = (mode_step_w ? step_pulse_w : ce_auto_r) & ~hold_ce;

    // -------------------------------------------------------------------------
    // S-CPU Core & MMIO
    // -------------------------------------------------------------------------
    wire        io_rd_w, io_wr_w;
    wire [2:0]  io_dev_w;
    wire [7:0]  io_reg_w;
    wire [15:0] io_wdata_w;
    wire [15:0] io_rdata_w;

    scpu_core u_cpu (
        .clk     (clk_core),
        .ce      (ce_w),
        .reset   (rst_sync_w),
        .io_rd   (io_rd_w),
        .io_wr   (io_wr_w),
        .io_dev  (io_dev_w),
        .io_reg  (io_reg_w),
        .io_wdata(io_wdata_w),
        .io_rdata(io_rdata_w)
    );

    // Device #0: LEDs + dual 7-seg
    wire [3:0]  led4_w;
    wire [15:0] io_rdata_led_w;

    mmio_device u_dev0 (
        .clk     (clk_core),
        .reset   (rst_sync_w),
        .io_rd   (io_rd_w),
        .io_wr   (io_wr_w),
        .io_dev  (io_dev_w),
        .io_reg  (io_reg_w),
        .io_wdata(io_wdata_w),
        .io_rdata(io_rdata_led_w),
        .led4    (led4_w),
        .seg     (o_digitalTube),
        .sel     (o_sel)
    );

    // IO read mux (extend for more devices)
    assign io_rdata_w = (io_rd_w && (io_dev_w==3'd0)) ? io_rdata_led_w
                                                      : 16'h0000;

    // -------------------------------------------------------------------------
    // Debug LEDs (active-LOW on board)
    // -------------------------------------------------------------------------
    wire show_msb_w = ~i_switch[3];
    wire show_pc_w  = ~i_button[3];
    wire show_ir_w  = ~i_button[2];
    wire show_ac_w  = ~i_button[1];

    // Internal CPU registers for debug
    wire [15:0] pc_w = u_cpu.PC;
    wire [15:0] ir_w = u_cpu.IR;
    wire [15:0] ac_w = u_cpu.ACC;

    wire [7:0] pc_lo_w = pc_w[7:0];
    wire [7:0] pc_hi_w = pc_w[15:8];
    wire [7:0] ir_lo_w = ir_w[7:0];
    wire [7:0] ir_hi_w = ir_w[15:8];
    wire [7:0] ac_lo_w = ac_w[7:0];
    wire [7:0] ac_hi_w = ac_w[15:8];

    wire [7:0] leds_active_high_w =
        show_pc_w   ? (show_msb_w ? pc_hi_w : pc_lo_w) :
        show_ac_w   ? (show_msb_w ? ac_hi_w : ac_lo_w) :
        show_ir_w   ? (show_msb_w ? ir_hi_w : ir_lo_w) :
        mode_step_w ? { u_cpu.STEP, u_cpu.CARRY, u_cpu.INDIRECTED, 1'b0, led4_w } :
                      { 4'b0, led4_w };

    // Drive board LEDs (active-LOW)
    assign o_led = ~leds_active_high_w;

endmodule

// -----------------------------------------------------------------------------
// Simple 2-flop synchronizer (async input -> clk domain)
// -----------------------------------------------------------------------------
module sync_2ff (
    input  wire clk,
    input  wire d_async,
    output reg  q
);
    reg meta;
    always @(posedge clk) begin
        meta <= d_async;
        q    <= meta;
    end
endmodule
