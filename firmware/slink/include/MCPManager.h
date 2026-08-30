#ifndef MCPManager_h
#define MCPManager_h

#include "MCP23S17.h"

/**
 * MCP23S17 ADDRESSING SCHEME (SPI)
 * ================================
 * Three MCP23S17 expanders on shared SPI bus with unique addresses:
 * - MCP_CTRL  (0x20): Control signals (power, reset, clock, programming, transceiver control, WE/OE)
 * - MCP_ADDR  (0x21): 16-bit address bus (Port A = bits 0-7 LSB, Port B = bits 8-15 MSB) → 74LS245 → ROM/RAM addr
 * - MCP_DATA  (0x22): 16-bit data bus bidirectional (Port A = bits 0-7 LSB, Port B = bits 8-15 MSB) → 74LS245 → ROM/RAM data
 * 
 * Convention: GPIO Port A = LSB (bits 0-7), GPIO Port B = MSB (bits 8-15)
 * SPI Speed: Up to 10 MHz for parallel access (10x faster than I²C PCF8574 + 74HC595 serial shifting)
 */

// ============ MCP_CTRL: Control Signal Pins (MCP address 0x20) ============
#define CTRL_PIN_PROG_EN         0  // Active HIGH - Enable programming mode: routes MCP_ADDR to ROM address, disconnects PC/IR
#define CTRL_PIN_RESET           1  // Active LOW  - Master reset signal (pulse 50 ms)
#define CTRL_PIN_CLK_SRC         2  // Binary - Clock source selection: LOW = NE555, HIGH = ESP32 PWM
#define CTRL_PIN_PSU_RELAY       3  // Active LOW  - Power supply relay: LOW = power ON, HIGH = power OFF

#define CTRL_PIN_DATA_TRX_EN     4  // Active LOW  - Data transceiver enable (74LS245 ~E): LOW = active, HIGH = inactive
#define CTRL_PIN_DATA_TRX_DIR    6  // Binary - Transceiver direction: LOW = MCP→bus (write), HIGH = bus→MCP (read)
                                      // IMPORTANT: Requires setDataBusMode() to reconfigure MCP_DATA pins as OUTPUT or INPUT

// Note: CTRL_PIN_ADDR_TRX_EN is implicitly controlled by PROG_EN (both tied to 74LS245 ~E for address bus)
// Note: CTRL_PIN_ADDR_TRX_DIR is not used (always tied to GND = MCP→bus, direction always MCP to ROM/RAM)

#define CTRL_PIN_ROM_WE          8  // Active LOW  - ROM (SST39) write enable: LOW = pulse write, HIGH = idle
#define CTRL_PIN_ROM_OE          9  // Active HIGH - ROM (SST39) output enable: HIGH = outputs on bus, LOW = disabled
                                      // Note: Inverted by 74x04 logic gate on hardware

#define CTRL_PIN_RAM_OE         12  // Active HIGH - RAM (UT6264C) output enable: HIGH = outputs on bus, LOW = disabled
                                      // Note: Inverted by 74x04 logic gate on hardware
#define CTRL_PIN_RAM_WE         13  // Active LOW  - RAM (UT6264C) write enable: LOW = pulse write, HIGH = idle

// ============ MCP Addresses (SPI slave addresses) ============
#define MCP_ADDR_CTRL  0  // Control signals expander
#define MCP_ADDR_ADDR  1  // Address bus expander
#define MCP_ADDR_DATA  2  // Data bus expander

enum DataBusMode {
    DATA_BUS_WRITE = 0,     // SLink drives data bus (MCP_DATA as OUTPUT)
    DATA_BUS_READ = 1       // SLink reads data from bus (MCP_DATA as INPUT)
};

/**
 * MCPManager: High-level interface for MCP23S17 expanders.
 * 
 * Manages three MCP23S17 devices over shared SPI bus:
 * - Control signals (PROG_EN, RESET, WE/OE for ROM and RAM)
 * - 16-bit address bus to ROM/RAM
 * - 16-bit bidirectional data bus to ROM/RAM
 * 
 * Error Handling:
 * - SPI errors are logged and retried once (transient SPI glitches)
 * - Critical failures (initialization) return false
 * - Runtime errors logged as ERROR level (not silently ignored)
 */
class MCPManager {
    public:
        MCPManager();

        // Initialization: returns false on SPI errors or hardware issues
        bool begin();

        // Control signal operations (with error checking)
        uint8_t readCtrl(uint8_t pin);          // Returns 0 or 1, or 0xFF on SPI error
        bool setCtrl(uint8_t pin, uint8_t value); // Returns false on SPI error (logged)

        // Address/Data bus operations (with error checking)
        bool writeAddress(uint16_t addr);       // Set 16-bit address on bus (via MCP_ADDR)
        bool writeData(uint16_t data);          // Write 16-bit data to bus (via MCP_DATA, must be in WRITE mode)
        uint16_t readData();                    // Read 16-bit data from bus (via MCP_DATA, must be in READ mode)
        
        // Data bus mode switching (requires GPIO reconfiguration)
        void setDataBusMode(DataBusMode mode);  // Switch data bus mode: reconfigures MCP_DATA GPIO direction
        DataBusMode getDataBusMode();           // Get current data bus mode
        
        // Utility for debugging
        bool isReady();                         // Returns true if MCP is initialized and responsive

    private:
        MCP23S17 _mcpAddr;      // Address bus expander (16-bit output)
        MCP23S17 _mcpData;      // Data bus expander (16-bit bidirectional)
        MCP23S17 _mcpCtrl;      // Control signals expander (multi-purpose)
        DataBusMode _dataBusMode; // Current data bus direction
        bool _initialized;      // Tracks successful initialization
        
        // Helper: SPI write with retry logic
        bool _spiWrite1WithRetry(MCP23S17& mcp, uint8_t pin, uint8_t value);
        bool _spiWrite16WithRetry(MCP23S17& mcp, uint16_t data);
        bool _ensureInitialized();
        bool _ensureDataBusMode(DataBusMode mode);
};

#endif