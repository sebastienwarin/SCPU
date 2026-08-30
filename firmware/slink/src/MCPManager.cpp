#include "pins.h"
#include "MCPManager.h"

MCPManager::MCPManager()
  : _mcpAddr(PIN_CS, MCP_ADDR_ADDR),
    _mcpData(PIN_CS, MCP_ADDR_DATA),
    _mcpCtrl(PIN_CS, MCP_ADDR_CTRL),
    _dataBusMode(DATA_BUS_WRITE),
    _initialized(false)
{
  log_i("Constructor called");
}

bool MCPManager::begin() {
  log_i("Initialization starting...");
  
  // ========== Step 1: Initialize SPI Bus ==========
  log_v("Configuring SPI bus");
  SPI.begin(PIN_SCK, PIN_MISO, PIN_MOSI, -1);
  delay(10);  // Allow SPI bus to settle

  // ========== Step 2: Initialize MCP23S17 Expanders ==========
  log_v("Initializing MCP23S17 expanders (3 devices)");
  log_v("  - MCP_CTRL (addr 0x%02X): Control signals", MCP_ADDR_CTRL);
  log_v("  - MCP_ADDR (addr 0x%02X): 16-bit address bus", MCP_ADDR_ADDR);
  log_v("  - MCP_DATA (addr 0x%02X): 16-bit data bus", MCP_ADDR_DATA);
  
  if (!_mcpCtrl.begin(false)) {
    log_e("MCP_CTRL initialization failed");
    return false;
  }
  if (!_mcpAddr.begin(false)) {
    log_e("MCP_ADDR initialization failed");
    return false;
  }
  if (!_mcpData.begin(false)) {
    log_e("MCP_DATA initialization failed");
    return false;
  }
  delay(10);

  // ========== Step 3: Enable Hardware Addressing ==========
  log_v("Enabling hardware addressing");
  if (!_mcpCtrl.enableHardwareAddress()) {
    log_e("Failed to enable HW address on MCP_CTRL");
    return false;
  }
  if (!_mcpAddr.enableHardwareAddress()) {
    log_e("Failed to enable HW address on MCP_ADDR");
    return false;
  }
  if (!_mcpData.enableHardwareAddress()) {
    log_e("Failed to enable HW address on MCP_DATA");
    return false;
  }
  delay(10);

  // ========== Step 4: Configure MCP_CTRL (Control Signals) ==========
  log_v("Configuring MCP_CTRL: all GPIO as OUTPUT");
  _mcpCtrl.pinMode16(0x0000);  // All 16 pins as OUTPUT
  _mcpCtrl.reverse16ByteOrder(true);
  
  // Set default control state (all inactive/safe)
  uint16_t ctrlDefault = 0;
  ctrlDefault |= (1 << CTRL_PIN_RESET);       // Active LOW → set to HIGH (no reset)
  ctrlDefault |= (1 << CTRL_PIN_PSU_RELAY);   // Active LOW → set to HIGH (power OFF)
  ctrlDefault |= (1 << CTRL_PIN_DATA_TRX_EN); // Active LOW → set to HIGH (disabled)
  ctrlDefault |= (1 << CTRL_PIN_ROM_WE);      // Active LOW → set to HIGH (not writing)
  ctrlDefault |= (1 << CTRL_PIN_RAM_WE);      // Active LOW → set to HIGH (not writing)
  ctrlDefault |= (1 << CTRL_PIN_ROM_OE);      // Active HIGH → set to HIGH (ROM output disabled is correct)
  ctrlDefault |= (1 << CTRL_PIN_RAM_OE);      // Active HIGH → set to HIGH (RAM output disabled is correct)
  // PROG_EN defaults to LOW (programming mode disabled)
  // CLK_SRC defaults to LOW (NE555 clock)
  // DATA_TRX_DIR defaults to LOW (write mode)
  
  if (!_spiWrite16WithRetry(_mcpCtrl, ctrlDefault)) {
    log_e("Failed to initialize MCP_CTRL state");
    return false;
  }
  log_v("MCP_CTRL initialized");
  
  // ========== Step 5: Configure MCP_ADDR (Address Bus) ==========
  log_v("Configuring MCP_ADDR: all GPIO as OUTPUT");
  _mcpAddr.pinMode16(0x0000);   // All 16 pins as OUTPUT
  _mcpAddr.reverse16ByteOrder(true);
  if (!_spiWrite16WithRetry(_mcpAddr, 0x0000)) {
    log_e("Failed to initialize MCP_ADDR");
    return false;
  }
  log_v("MCP_ADDR initialized");

  // ========== Step 6: Configure MCP_DATA (Data Bus, default OUTPUT) ==========
  log_v("Configuring MCP_DATA: all GPIO as OUTPUT");
  _mcpData.pinMode16(0x0000);   // All 16 pins as OUTPUT (write mode)
  _mcpData.reverse16ByteOrder(true);
  if (!_spiWrite16WithRetry(_mcpData, 0x0000)) {
    log_e("Failed to initialize MCP_DATA");
    return false;
  }
  _dataBusMode = DATA_BUS_WRITE;
  log_v("MCP_DATA initialized (mode: WRITE)");

  // ========== Initialization Complete ==========
  _initialized = true;
  log_i("Initialization complete");
  return true;
}

// ============ Control Signal Operations ============

uint8_t MCPManager::readCtrl(uint8_t pin) {
  if (!_ensureInitialized()) {
    return 0xFF;  // Error sentinel value
  }
  uint8_t value = _mcpCtrl.read1(pin);
  log_v("Read ctrl pin %d = 0x%02X", pin, value);
  return value;
}

bool MCPManager::setCtrl(uint8_t pin, uint8_t value) {
  if (!_ensureInitialized()) {
    return false;
  }
  if (!_spiWrite1WithRetry(_mcpCtrl, pin, value)) {
    log_e("Failed to set ctrl pin %d = %u", pin, value);
    return false;
  }
  log_v("Set ctrl pin %d = 0x%02X", pin, value);
  return true;
}

// ============ Address/Data Bus Operations ============

bool MCPManager::writeAddress(uint16_t addr) {
  if (!_ensureInitialized()) {
    return false;
  }
  if (!_spiWrite16WithRetry(_mcpAddr, addr)) {
    log_e("Failed to write address 0x%04X", addr);
    return false;
  }
  log_v("Write address 0x%04X", addr);
  return true;
}

bool MCPManager::writeData(uint16_t data) {
  if (!_ensureInitialized()) {
    return false;
  }
  if (!_ensureDataBusMode(DATA_BUS_WRITE)) {
    return false;  // STRICT: Don't write if not in write mode
  }
  if (!_spiWrite16WithRetry(_mcpData, data)) {
    log_e("Failed to write data 0x%04X", data);
    return false;
  }
  log_v("Write data 0x%04X", data);
  return true;
}

uint16_t MCPManager::readData() {
  if (!_ensureInitialized()) {
    return 0xFFFF;  // Error sentinel
  }
  if (!_ensureDataBusMode(DATA_BUS_READ)) {
    return 0xFFFF;  // STRICT: Fail if not in read mode
  }
  uint16_t data = _mcpData.read16();
  log_v("Read data 0x%04X", data);
  return data;
}

// ============ Data Bus Mode Switching ============

void MCPManager::setDataBusMode(DataBusMode mode) {
  if (!_ensureInitialized()) {
    return;
  }
  if (_dataBusMode == mode) {
    log_v("Already in %s mode",
          mode == DATA_BUS_WRITE ? "WRITE" : "READ");
    return;  // Already in requested mode
  }
  
  _dataBusMode = mode;
  
  if (mode == DATA_BUS_WRITE) {
    log_v("Switching data bus to WRITE mode");
    _mcpData.pinMode16(0x0000);  // All 16 pins as OUTPUT
  } else {
    log_v("Switching data bus to READ mode");
    _mcpData.pinMode16(0xFFFF);  // All 16 pins as INPUT
  }
}

DataBusMode MCPManager::getDataBusMode() {
  return _dataBusMode;
}

bool MCPManager::isReady() {
  return _initialized;
}

// ============ Private Helper Methods (SPI Retry Logic) ============

bool MCPManager::_spiWrite1WithRetry(MCP23S17& mcp, uint8_t pin, uint8_t value) {
  // Try once, retry once on failure (transient SPI glitches)
  for (int attempt = 0; attempt < 2; attempt++) {
    bool result = mcp.write1(pin, value);
    if (result) {
      return true;  // Success
    }
    if (attempt < 1) {
      log_w("Retrying SPI write on pin %d", pin);
      delayMicroseconds(100);  // Brief delay before retry
    }
  }
  log_e("SPI write failed after retries (pin=%d)", pin);
  return false;
}

bool MCPManager::_spiWrite16WithRetry(MCP23S17& mcp, uint16_t data) {
  // Try once, retry once on failure (transient SPI glitches)
  for (int attempt = 0; attempt < 2; attempt++) {
    bool result = mcp.write16(data);
    if (result) {
      return true;  // Success
    }
    if (attempt < 1) {
      log_w("Retrying SPI write 0x%04X", data);
      delayMicroseconds(100);  // Brief delay before retry
    }
  }
  log_e("SPI write failed after retries (0x%04X)", data);
  return false;
}

bool MCPManager::_ensureInitialized() {
  if (_initialized) {
    return true;
  }
  log_e("MCP manager is not initialized");
  return false;
}

bool MCPManager::_ensureDataBusMode(DataBusMode mode) {
  if (_dataBusMode == mode) {
    return true;
  }
  log_e("Data bus is not in %s mode", mode == DATA_BUS_WRITE ? "WRITE" : "READ");
  return false;
}