#ifndef SLink_h
#define SLink_h

#include "MCPManager.h"

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
        SLink(MCPManager& mcp) : _mcp(mcp) {}
        bool begin();
        bool getPowerState();
        void setPowerState(bool state);
        void masterReset();
        ClockInfo getClock();
        void setClock(ClockInfo clockInfo);
        void stopClock();
        void tick(bool fullCycle);
        void setProgrammerMode(bool enable, bool resetOnExit = false);
        bool getProgrammerMode();
        
    private:
        hw_timer_t *_timer = nullptr;
        MCPManager& _mcp;
        ClockInfo _clockInfo = { SLINK_CLOCK, DEFAULT_FREQUENCY, false };
        bool _progrEnabled = false;
        bool _powerState = false;

        bool _requirePowerOn(const char* action);
        bool _requireProgrammingDisabled(const char* action);
};

#endif
