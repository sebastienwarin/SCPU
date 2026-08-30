#include "FlashProgrammer.h"

namespace {
  const unsigned long SST39_PROGRAM_TIMEOUT_MS = 500;
  const unsigned long SST39_ERASE_TIMEOUT_MS = 10000;
}

bool FlashProgrammer::begin() {
  log_i("Initialization");
  return true;
}

bool FlashProgrammer::getState() {
  return _enabled;
}

void FlashProgrammer::setState(bool enable, bool writeMode) {
  if(enable) {
    if(writeMode) {
      // ========== Enter WRITE mode (ROM Programming) ==========
      log_i("Entering WRITE mode");
      
      // Ensure ROM output is disabled (safety)
      if (!_mcp.setCtrl(CTRL_PIN_ROM_OE, LOW)) {
        log_e("Failed to disable ROM_OE");
        return;
      }
      delayMicroseconds(1);
      
      // Configure data bus for MCP output
      if (!_mcp.setCtrl(CTRL_PIN_DATA_TRX_DIR, LOW)) {   // LOW = SLink → Bus (write)
        log_e("Failed to set DATA_TRX_DIR");
        return;
      }
      if (!_mcp.setCtrl(CTRL_PIN_DATA_TRX_EN, LOW)) {    // Active LOW = enabled
        log_e("Failed to enable DATA_TRX_EN");
        return;
      }
      _mcp.setDataBusMode(DATA_BUS_WRITE);
      
      delayMicroseconds(1);
      _writeMode = true;
      _enabled = true;
      log_i("WRITE mode active");
    }
    else {
      // ========== Enter READ mode (ROM Readback) ==========
      log_i("Entering READ mode");
      
      // Configure data bus for bus input to MCP
      if (!_mcp.setCtrl(CTRL_PIN_DATA_TRX_DIR, HIGH)) {  // HIGH = Bus → SLink (read)
        log_e("Failed to set DATA_TRX_DIR");
        return;
      }
      if (!_mcp.setCtrl(CTRL_PIN_DATA_TRX_EN, LOW)) {    // Active LOW = enabled
        log_e("Failed to enable DATA_TRX_EN");
        return;
      }
      _mcp.setDataBusMode(DATA_BUS_READ);
      
      delayMicroseconds(1);
      
      // Enable ROM output (allow ROM to drive data bus)
      if (!_mcp.setCtrl(CTRL_PIN_ROM_OE, HIGH)) {        // Active HIGH = enabled
        log_e("Failed to enable ROM_OE");
        return;
      }
      
      delayMicroseconds(1);
      _writeMode = false;
      _enabled = true;
      log_i("READ mode active");
    }
  }
  else {
    // ========== Disable Programming Mode (Safe State) ==========
    log_i("Disabling programming mode");
    
    // Disable data transceiver
    if (!_mcp.setCtrl(CTRL_PIN_DATA_TRX_EN, HIGH)) {     // Active LOW → set HIGH = disabled
      log_w("Failed to disable DATA_TRX_EN");
    }
    
    // Disable ROM output
    if (!_mcp.setCtrl(CTRL_PIN_ROM_OE, LOW)) {           // Active HIGH → set LOW = disabled
      log_w("Failed to disable ROM_OE");
    }

    // Disable RAM output
    if (!_mcp.setCtrl(CTRL_PIN_RAM_OE, LOW)) {           // Active HIGH → set LOW = disabled
      log_w("Failed to disable RAM_OE");
    }
    
    // Reset data bus to write mode (safe default)
    _mcp.setDataBusMode(DATA_BUS_WRITE);
    
    _enabled = false;
    _writeMode = false;
    log_i("Programming mode disabled");
  }
}

bool FlashProgrammer::eraseChip() {
  if(!_requireWriteMode()) {
    return false;
  }
  
  log_i("[FlashProgrammer::eraseChip] Starting ROM erase (chip-erase command)");
  
  // SST39 Chip-Erase Sequence (Unlocking Bypass required)
  if(!_writeData(0x5555, 0xAAAA) ||
     !_writeData(0x2AAA, 0x5555) ||
     !_writeData(0x5555, 0x8080) ||
     !_writeData(0x5555, 0xAAAA) ||
     !_writeData(0x2AAA, 0x5555) ||
     !_writeData(0x5555, 0x1010)) {
    log_e("Failed to send ROM erase command sequence");
    return false;
  }

  if(!_waitEraseComplete(SST39_ERASE_TIMEOUT_MS)) {
    log_e("Timed out waiting for ROM erase completion");
    return false;
  }

  log_i("[FlashProgrammer::eraseChip] Erase completed");
  return true;
}

bool FlashProgrammer::programData(uint16_t address, uint16_t data) {
  if(!_requireWriteMode()) {
    return false;
  }

  // Keep RAM disconnected from data bus during ROM programming.
  if (!_mcp.setCtrl(CTRL_PIN_RAM_OE, LOW)) {
    log_e("Failed to disable RAM_OE");
    return false;
  }
  
  // SST39 Byte-Program Sequence (Unlocking Bypass required)
  if(!_writeData(0x5555, 0xAAAA) ||
     !_writeData(0x2AAA, 0x5555) ||
     !_writeData(0x5555, 0xA0A0) ||
     !_writeData(address, data)) {
    log_e("Failed program command sequence at 0x%04X", address);
    return false;
  }

  delayMicroseconds(30);  // SST39 byte-program minimum pulse time
  if(!_waitRomWord(address, data, SST39_PROGRAM_TIMEOUT_MS)) {
    log_e("Timed out waiting for program completion at 0x%04X", address);
    return false;
  }
  
  log_v("Wrote 0x%04X to 0x%04X", data, address);
  return true;
}

uint16_t FlashProgrammer::readRom(uint16_t address) {
  if(!_requireReadMode()) {
    return 0xFFFF;
  }

  // Ensure only ROM can drive the data bus for this read.
  if (!_mcp.setCtrl(CTRL_PIN_RAM_OE, LOW)) {
    log_e("Failed to disable RAM_OE");
    return 0xFFFF;
  }
  if (!_mcp.setCtrl(CTRL_PIN_ROM_OE, HIGH)) {
    log_e("Failed to enable ROM_OE");
    return 0xFFFF;
  }
  
  // ========== ROM Readback Sequence ==========
  // 1. Place address on bus
  if (!_mcp.writeAddress(address)) {
    log_e("Failed to write address 0x%04X", address);
    return 0xFFFF;
  }
  
  // 2. Wait for address setup time (SST39 min 1 µs)
  delayMicroseconds(1);
  
  // 3. Read data from ROM output
  uint16_t data = _mcp.readData();
  
  log_v("Read 0x%04X from 0x%04X", data, address);
  
  return data;
}

uint16_t FlashProgrammer::readRam(uint16_t address) {
  if(!_requireReadMode()) {
    return 0xFFFF;
  }

  // Ensure only RAM can drive the data bus.
  if (!_mcp.setCtrl(CTRL_PIN_ROM_OE, LOW)) {
    log_e("Failed to disable ROM_OE");
    return 0xFFFF;
  }
  if (!_mcp.setCtrl(CTRL_PIN_RAM_OE, HIGH)) {
    log_e("Failed to enable RAM_OE");
    return 0xFFFF;
  }

  if (!_mcp.writeAddress(address)) {
    log_e("Failed to write RAM address 0x%04X", address);
    _mcp.setCtrl(CTRL_PIN_RAM_OE, LOW);
    return 0xFFFF;
  }

  delayMicroseconds(1);
  uint16_t data = _mcp.readData();
  if (!_mcp.setCtrl(CTRL_PIN_RAM_OE, LOW)) {
    log_w("Failed to disable RAM_OE after read");
  }
  return data;
}

bool FlashProgrammer::writeRam(uint16_t address, uint16_t data) {
  if(!_requireWriteMode()) {
    return false;
  }

  // Keep outputs disabled while writing.
  if (!_mcp.setCtrl(CTRL_PIN_ROM_OE, LOW)) {
    log_e("Failed to disable ROM_OE");
    return false;
  }
  if (!_mcp.setCtrl(CTRL_PIN_RAM_OE, LOW)) {
    log_e("Failed to disable RAM_OE");
    return false;
  }

  if (!_mcp.writeAddress(address)) {
    log_e("Failed to write RAM address 0x%04X", address);
    return false;
  }

  if (!_mcp.writeData(data)) {
    log_e("Failed to write RAM data 0x%04X", data);
    return false;
  }

  if (!_mcp.setCtrl(CTRL_PIN_RAM_WE, LOW)) {
    log_e("Failed to pulse RAM_WE");
    return false;
  }
  delayMicroseconds(1);
  if (!_mcp.setCtrl(CTRL_PIN_RAM_WE, HIGH)) {
    log_e("Failed to release RAM_WE");
    return false;
  }

  return true;
}

bool FlashProgrammer::verifyRom(const uint8_t* buffer, uint16_t size) {
  if(!_requireReadMode()) {
    return false;
  }
  if (!buffer || size == 0) {
    log_e("Invalid verify buffer or size");
    return false;
  }
  
  log_i("Verifying ROM (%u bytes)", size);
  
  uint16_t mismatchCount = 0;
  uint16_t mismatchAddr = 0xFFFF;
  
  // Iterate through buffer, reading each word from ROM
  for (uint16_t addr = 0; addr < size; addr += 2) {
    // Read word from ROM
    uint16_t romData = readRom(addr / 2);
    
    // Extract word from buffer (LSB first). Handle odd file size safely.
    uint16_t lsb = buffer[addr];
    uint16_t msb = (addr + 1 < size) ? buffer[addr + 1] : 0;
    uint16_t bufferData = (msb << 8) | lsb;
    
    // Compare
    if (romData != bufferData) {
      // Retry once to filter transient bus-read glitches before flagging a mismatch.
      uint16_t confirmData = readRom(addr / 2);
      if (confirmData == bufferData) {
        continue;
      }

      if (mismatchCount == 0) {
        mismatchAddr = addr / 2;  // First mismatch address
        log_e("Mismatch at 0x%04X: expected 0x%04X, got 0x%04X",
              mismatchAddr, bufferData, confirmData);
      }
      mismatchCount++;
    }
  }
  
  if (mismatchCount == 0) {
    log_i("Verification passed");
    return true;
  } else {
    log_e("Verification failed: %u mismatches",
          mismatchCount);
    return false;
  }
}

bool FlashProgrammer::_writeData(uint16_t address, uint16_t data) {
  // SST39 write sequence: place address on bus, place data on bus, pulse WE
  if (!_mcp.writeAddress(address)) {
    log_e("Failed to write address 0x%04X", address);
    return false;
  }
  
  if (!_mcp.writeData(data)) {
    log_e("Failed to write data 0x%04X", data);
    return false;
  }
  
  // Pulse ROM_WE (Active LOW: LOW for 1 µs, then back to HIGH)
  if (!_mcp.setCtrl(CTRL_PIN_ROM_WE, LOW)) {
    log_e("Failed to pulse ROM_WE");
    return false;
  }
  delayMicroseconds(1);
  if (!_mcp.setCtrl(CTRL_PIN_ROM_WE, HIGH)) {
    log_e("Failed to release ROM_WE");
    return false;
  }

  return true;
}

bool FlashProgrammer::_requireReadMode() {
  if(!_enabled) {
    log_e("Programmer is not enabled");
    return false;
  }
  if(_writeMode) {
    log_e("Programmer is not in READ mode");
    return false;
  }
  return true;
}

bool FlashProgrammer::_requireWriteMode() {
  if(!_enabled) {
    log_e("Programmer is not enabled");
    return false;
  }
  if(!_writeMode) {
    log_e("Programmer is not in WRITE mode");
    return false;
  }
  return true;
}

bool FlashProgrammer::_enterRomReadBusMode() {
  if (!_mcp.setCtrl(CTRL_PIN_RAM_OE, LOW)) {
    return false;
  }
  if (!_mcp.setCtrl(CTRL_PIN_DATA_TRX_DIR, HIGH)) {
    return false;
  }
  if (!_mcp.setCtrl(CTRL_PIN_DATA_TRX_EN, LOW)) {
    return false;
  }
  _mcp.setDataBusMode(DATA_BUS_READ);
  delayMicroseconds(1);
  if (!_mcp.setCtrl(CTRL_PIN_ROM_OE, HIGH)) {
    return false;
  }
  return true;
}

bool FlashProgrammer::_enterWriteBusMode() {
  if (!_mcp.setCtrl(CTRL_PIN_ROM_OE, LOW)) {
    return false;
  }
  if (!_mcp.setCtrl(CTRL_PIN_RAM_OE, LOW)) {
    return false;
  }
  if (!_mcp.setCtrl(CTRL_PIN_DATA_TRX_DIR, LOW)) {
    return false;
  }
  if (!_mcp.setCtrl(CTRL_PIN_DATA_TRX_EN, LOW)) {
    return false;
  }
  _mcp.setDataBusMode(DATA_BUS_WRITE);
  delayMicroseconds(1);
  return true;
}

bool FlashProgrammer::_waitRomWord(uint16_t address, uint16_t expected, unsigned long timeoutMs) {
  if(!_enterRomReadBusMode()) {
    log_e("Failed to enter read mode during polling");
    _enterWriteBusMode();
    return false;
  }

  unsigned long start = millis();
  bool matched = false;
  while ((millis() - start) <= timeoutMs) {
    if (!_mcp.writeAddress(address)) {
      log_e("Failed to write poll address 0x%04X", address);
      break;
    }
    delayMicroseconds(1);
    uint16_t current = _mcp.readData();
    if (current == expected) {
      matched = true;
      break;
    }
  }

  if(!_enterWriteBusMode()) {
    log_e("Failed to restore write mode after polling");
    return false;
  }

  return matched;
}

bool FlashProgrammer::_waitEraseComplete(unsigned long timeoutMs) {
  return _waitRomWord(0x0000, 0xFFFF, timeoutMs);
}