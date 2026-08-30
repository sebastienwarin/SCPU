#ifndef SLink_h
#define SLink_h

#include "PCF8574.h"

#define PROG_EN     P0  // Active HIGH
#define RESET       P1  // Active LOW
#define CLK_SRC     P2
#define PSU_RELAY   P7  // Active LOW

#define PWM_CHANNEL 0

#define MASTER_RESET_PULSE_MS   50       // ms

#define DEFAULT_FREQUENCY   2000000     // 2 MHz
#define MAX_FREQUENCY       5000000     // 5 MHz

enum ClockSource { NE555_CLOCK = 0, SLINK_CLOCK = 1 };
struct ClockInfo {
    ClockSource Source;
    long Frequency;
    bool Auto;
};

class SLink {
    public:
        SLink(uint8_t address);
        bool begin();
        bool getPowerState();
        void setPowerState(bool state);
        void masterReset();
        ClockInfo getClock();
        void setClock(ClockInfo clockInfo);
        void stopClock();
        void tick(bool fullCycle);
        void setProgrammerMode(bool enable);
        bool getProgrammerMode();
        
    private:
        hw_timer_t *_timer = nullptr;
        PCF8574 *_pcf = nullptr;
        ClockInfo _clockInfo = { SLINK_CLOCK, DEFAULT_FREQUENCY, false };
        bool _progrEnabled = false;
        bool _powerState = false;
};

#endif