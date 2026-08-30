#include "pins.h"
#include "FlashProgrammer.h"

FlashProgrammer::FlashProgrammer(uint8_t address) {
  _pcf = new PCF8574(address);
}

bool FlashProgrammer::begin() {
  // Init PCF8574 I/O
  for (uint8_t i = 0; i < 8; i++) {
    _pcf->pinMode(i, OUTPUT, LOW);
  }
  return _pcf->begin();
}

void FlashProgrammer::setOutput(bool enable) {
  for (uint8_t i = 0; i < 8; i++) {
    if(enable) {
      _pcf->digitalWrite(i, i == ROM_OE ? LOW : HIGH);
    }
    else {
      _pcf->digitalWrite(i, LOW);
    }
    _outputEnabled = enable;
  }
}

bool FlashProgrammer::getState() {
  return _enabled;
}

void FlashProgrammer::setState(bool enable, bool rw) {
  if(!_outputEnabled) {
    log_e("GPIO outputs are disabled!");
  }
  else if(enable) {
    if(rw) {      
      // Ensure ROM output is disabled
      _pcf->digitalWrite(ROM_OE, LOW);
      delayMicroseconds(1);
      // Enable data register output
      _pcf->digitalWrite(DATA_OE, LOW);
    }
    else {
      // Disable data register output
      _pcf->digitalWrite(DATA_OE, HIGH);
      delayMicroseconds(1);
      // Enable ROM output
      _pcf->digitalWrite(ROM_OE, HIGH);
    }
    delayMicroseconds(1);
    // Done
    _rwMode = rw;
    _enabled = true;
    log_i("FlashProgrammer enabled mode: %s", (rw ? "RW" : "RO"));
  }
  else {
    // Disable data register & ROM output
    _pcf->digitalWrite(DATA_OE, HIGH);  // Active LOW
    _pcf->digitalWrite(ROM_OE, LOW);    // Active HIGH
    // Done  
    _enabled = false;
  }
}

void FlashProgrammer::eraseChip() {
  if(!_enabled) {
    log_e("FlashProgrammer disabled!");
  }
  else if(!_rwMode) {
    log_e("FlashProgrammer is enabled in read-only!");
  }
  else {
    log_d("Erasing ROM data ...");
    _writeData(0x5555, 0xAAAA);
    _writeData(0x2AAA, 0x5555);
    _writeData(0x5555, 0x8080);
    _writeData(0x5555, 0xAAAA);
    _writeData(0x2AAA, 0x5555);
    _writeData(0x5555, 0x1010);
    log_i("Erasing ROM completed!");
  }
}

void FlashProgrammer::programData(uint16_t address, uint16_t data) {
  if(!_enabled) {
    log_e("FlashProgrammer disabled!");
  }
  else if(!_rwMode) {
    log_e("FlashProgrammer is enabled in read-only!");
  }
  else {
    log_i("write %d = %d", address, data);
    // Byte-Program command
    _writeData(0x5555, 0xAAAA);
    _writeData(0x2AAA, 0x5555);
    _writeData(0x5555, 0xA0A0);
    // Write data
    _writeData(address, data);
    delayMicroseconds(30);
  }
}

void FlashProgrammer::outputData(uint16_t address, bool enable) {
  if(!_enabled) {
    log_e("FlashProgrammer disabled!");
  }
  else if(_rwMode) {
    log_e("FlashProgrammer is not enabled in read-only!");
  }
  else {
    log_i("outputData = %d for address %d", enable, address);
     _setAddress(address);
    _pcf->digitalWrite(ROM_OE, enable ? HIGH : LOW);
  }
}

void FlashProgrammer::_writeData(uint16_t address, uint16_t data) {
  // Ensure ROM output is disabled
  _pcf->digitalWrite(ROM_OE, LOW);
  // Set address & data to shift registers
  _setAddress(address);
  _setData(data);
  // Send WE signal for 1uS
  _pcf->digitalWrite(ROM_WE, LOW);
  delayMicroseconds(1);
  _pcf->digitalWrite(ROM_WE, HIGH);
}

void FlashProgrammer::_setAddress(uint16_t address) {
  _shiftData(ADDR_RCLK, ADDR_SRCLK, address);  
}

void FlashProgrammer::_setData(uint16_t data) {
  _shiftData(DATA_RCLK, DATA_SRCLK, data);
}

void FlashProgrammer::_shiftData(int8_t rclkPin, int8_t srclkPin, uint16_t data) {
  _pcf->digitalWrite(rclkPin, LOW);
  for (int8_t i = 0; i < 16; i++) {
    int8_t value = 1 & (data >> i);
    _pcf->digitalWrite(srclkPin, LOW);
    _pcf->digitalWrite(SERIAL_DATA, value);
    _pcf->digitalWrite(srclkPin, HIGH);
  }
  _pcf->digitalWrite(rclkPin, HIGH);
}