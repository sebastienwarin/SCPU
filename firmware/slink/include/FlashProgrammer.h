#ifndef FlashProgrammer_h
#define FlashProgrammer_h

#include "MCPManager.h"

/**
 * FlashProgrammer: Low-level SST39 Flash and UT6264C RAM control.
 * 
 * Handles:
 * - SST39SF ROM programming (chip erase, byte-program) via MCP_ADDR/MCP_DATA
 * - ROM readback for verification (byte-perfect compare)
 * - RAM read/write operations (future)
 * 
 * State Machine:
 * - setState(false) → Disabled (safe state)
 * - setState(true, true) → Write mode (ROM programming, RAM write)
 * - setState(true, false) → Read mode (ROM readback, RAM read)
 * 
 * SST39 Timing:
 * - Byte-program: 30 µs minimum
 * - Chip erase: ~10 seconds
 * - Address setup: ~1 µs
 */
class FlashProgrammer {
    public:
        FlashProgrammer(MCPManager& mcp) : _mcp(mcp) {}
        
        bool begin();
        bool getState();
        
        // Programming mode control
        void setState(bool enable, bool writeMode = false);
        
        // ROM Operations
        bool eraseChip();                                    // Erase entire SST39 ROM
        bool programData(uint16_t address, uint16_t data);   // Program one word to ROM (30µs)
        uint16_t readRom(uint16_t address);                  // Read one word from ROM (for verification)
        bool verifyRom(const uint8_t* buffer, uint16_t size); // Verify entire ROM against buffer
        
        // RAM Operations
        uint16_t readRam(uint16_t address);                // Read one word from RAM
        bool writeRam(uint16_t address, uint16_t data);    // Write one word to RAM

    private:
        MCPManager& _mcp;
        bool _enabled = false;      // Is programmer mode active?
        bool _writeMode = false;    // Are we in write mode (vs read mode)?
        
        // Helper: Execute SST39 command sequence
        bool _writeData(uint16_t address, uint16_t data);
        bool _requireReadMode();
        bool _requireWriteMode();
        bool _enterRomReadBusMode();
        bool _enterWriteBusMode();
        bool _waitRomWord(uint16_t address, uint16_t expected, unsigned long timeoutMs);
        bool _waitEraseComplete(unsigned long timeoutMs);
};

#endif