#ifndef FlashProgrammer_h
#define FlashProgrammer_h

#include "PCF8574.h"

#define SERIAL_DATA P0
#define ADDR_RCLK   P1
#define ADDR_SRCLK  P2
#define DATA_RCLK   P3
#define DATA_SRCLK  P4
#define DATA_OE     P5  // Active LOW
#define ROM_OE      P6  // Active HIGH
#define ROM_WE      P7  // Active LOW

class FlashProgrammer {
    public:
        FlashProgrammer(uint8_t address);
        bool begin();
        void setOutput(bool enable);
        bool getState();
        void setState(bool enable, bool rw = false);
        void eraseChip();
        void programData(uint16_t address, uint16_t data);
        void outputData(uint16_t address, bool enable);

    private:
        PCF8574 *_pcf = nullptr;
        bool _outputEnabled = false;
        bool _enabled = false;
        bool _rwMode = false;
        void _setAddress(uint16_t address);
        void _setData(uint16_t data);
        void _writeData(uint16_t address, uint16_t data);
        void _shiftData(int8_t rclkPin, int8_t srclkPin, uint16_t data);
};

#endif