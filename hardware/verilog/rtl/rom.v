//------------------------------------------------------------------------------
// 64Kx16 ROM initialized via $readmemh
// INIT_FILE can be overridden at elaboration time.
//------------------------------------------------------------------------------

module rom #(
    parameter integer AW = 16,
    parameter        INIT_FILE = "../sim/rom.hex"
)(
    input  wire [AW-1:0] addr,
    output wire [15:0]   dout
);
    localparam integer DEPTH = (1<<AW);

    reg [15:0] mem [0:DEPTH-1];

    initial begin
        if (INIT_FILE != "") begin
            $display("ROM: loading %s", INIT_FILE);
            $readmemh(INIT_FILE, mem);
        end
    end

    assign dout = mem[addr];
endmodule