// -----------------------------------------------------------------------------
// btn_onepulse.v
// Debounced one-pulse generator for an active-LOW push button.
// - DEBOUNCE is the required stability (in clock cycles) before the state is
//   considered changed.
// - Produces a single-cycle HIGH pulse on each *press* (LOW->HIGH internally).
// -----------------------------------------------------------------------------

module btn_onepulse #(
    parameter integer DEBOUNCE = 250_000  // ~5 ms @ 50 MHz (adjust to your i_clk)
)(
    input  wire i_clk,
    input  wire i_rst,    // active-HIGH synchronous reset
    input  wire i_btn_n,  // active-LOW button (use ~i_btn if active-HIGH)
    output wire o_pulse   // 1 cycle HIGH on each press
);
    // 1) Synchronize and invert (active-low -> active-high internally)
    reg [1:0] sync_r = 2'b00;
    always @(posedge i_clk) begin
        if (i_rst) sync_r <= 2'b00;
        else       sync_r <= {sync_r[0], ~i_btn_n};
    end

    // 2) Debounce by requiring DEBOUNCE stable cycles before accepting a change
    localparam integer CNTW = (DEBOUNCE <= 1) ? 1 : $clog2(DEBOUNCE);
    reg [CNTW-1:0] db_cnt_r = {CNTW{1'b0}};
    reg            stable_r = 1'b0;

    always @(posedge i_clk) begin
        if (i_rst) begin
            db_cnt_r  <= {CNTW{1'b0}};
            stable_r  <= 1'b0;
        end else begin
            if (sync_r[1] != stable_r) begin
                if (db_cnt_r == DEBOUNCE-1) begin
                    stable_r <= sync_r[1];
                    db_cnt_r <= {CNTW{1'b0}};
                end else begin
                    db_cnt_r <= db_cnt_r + 1'b1;
                end
            end else begin
                db_cnt_r <= {CNTW{1'b0}};
            end
        end
    end

    // 3) Rising edge detect on the debounced signal -> one pulse
    reg stable_d_r = 1'b0;
    always @(posedge i_clk) begin
        if (i_rst) stable_d_r <= 1'b0;
        else       stable_d_r <= stable_r;
    end

    assign o_pulse = stable_r & ~stable_d_r;

endmodule