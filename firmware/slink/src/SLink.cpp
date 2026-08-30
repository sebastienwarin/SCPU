#include "pins.h"
#include "SLink.h"

bool clockState = false;
bool isPwmActive = false;
void IRAM_ATTR toogleClock() {
  clockState = !clockState;
  digitalWrite(PIN_CLK, clockState ? HIGH : LOW);
}

bool SLink::begin() {
  log_i("SLink begin() called");
  
  // Init Clock signal GPIO
  pinMode(PIN_CLK, OUTPUT);
  digitalWrite(PIN_CLK, LOW);
  return true;
}

bool SLink::getPowerState() {
  return _powerState;
}

bool SLink::_requirePowerOn(const char* action) {
  if(_powerState) {
    return true;
  }
  log_e("Cannot %s while power is off", action);
  return false;
}

bool SLink::_requireProgrammingDisabled(const char* action) {
  if(!_progrEnabled) {
    return true;
  }
  log_e("Cannot %s while programming mode is enabled", action);
  return false;
}

void SLink::setPowerState(bool state) {
  if(!_requireProgrammingDisabled("change power state")) {
    return;
  }
  if(state) {
    // Switch relay on
    _mcp.setCtrl(CTRL_PIN_PSU_RELAY, LOW);
    _powerState = true;
    // Set GPIO to initial state
    _mcp.setCtrl(CTRL_PIN_CLK_SRC, HIGH);
    _mcp.setCtrl(CTRL_PIN_RESET, HIGH);
    _mcp.setCtrl(CTRL_PIN_PROG_EN, LOW);
  }
  else {
    // Stop clock
    stopClock();
    // Turn off GPIO
    _mcp.setCtrl(CTRL_PIN_PROG_EN, LOW);
    _mcp.setCtrl(CTRL_PIN_RESET, LOW);
    _mcp.setCtrl(CTRL_PIN_CLK_SRC, LOW);
    // Switch off relay
    _mcp.setCtrl(CTRL_PIN_PSU_RELAY, HIGH);
    _powerState = false;
  }
}

void SLink::masterReset() {
  if(!_requireProgrammingDisabled("reset the S-CPU") || !_requirePowerOn("reset")) {
    return;
  }

  _mcp.setCtrl(CTRL_PIN_RESET, LOW);
  delay(MASTER_RESET_PULSE_MS);
  _mcp.setCtrl(CTRL_PIN_RESET, HIGH);
}

ClockInfo SLink::getClock() {
  return _clockInfo;
}
void SLink::setClock(ClockInfo clockInfo) {
  if(!_requireProgrammingDisabled("change clock") || !_requirePowerOn("change clock")) {
    return;
  }

  _clockInfo = clockInfo;
  // Clock source
  _mcp.setCtrl(CTRL_PIN_CLK_SRC, clockInfo.Source);
  // S-Link clock generation
  if(clockInfo.Source == SLINK_CLOCK && clockInfo.Auto && clockInfo.Frequency > 0 && clockInfo.Frequency <= MAX_FREQUENCY) {
    // Select optimal resolution based on requested frequency
    uint8_t resolution;
    if (clockInfo.Frequency <= 10)        resolution = 14;
    else if (clockInfo.Frequency <= 100)  resolution = 12;
    else if (clockInfo.Frequency <= 1000) resolution = 10;
    else if (clockInfo.Frequency <= 10000) resolution = 8;
    else if (clockInfo.Frequency <= 50000) resolution = 6;
    else if (clockInfo.Frequency <= 100000) resolution = 4;
    else resolution = 1; // for >= 200 kHz
    // Initialize PWM channel
    uint32_t frequency = ledcSetup(PWM_CHANNEL, clockInfo.Frequency, resolution);
    if (frequency == 0) {
      log_e("Unable to configure PWM at %lu Hz with %d-bit resolution", clockInfo.Frequency, resolution);
      return;
    }
    // Attach pin to PWM channel
    ledcAttachPin(PIN_CLK, PWM_CHANNEL);
    // Start PWM with duty
    uint32_t duty = (1 << resolution) / 2;
    ledcWrite(PWM_CHANNEL, duty);
    isPwmActive = true;
    log_i("PWM configured: requested %lu Hz, achieved %lu Hz, resolution %d bits, duty = %lu", clockInfo.Frequency, frequency, resolution, duty);
  }
  else {
    // Stop clock
    _clockInfo.Auto = false;
    if(isPwmActive) {
      ledcWrite(PWM_CHANNEL, 0);
      ledcDetachPin(PIN_CLK);
      isPwmActive = false;
      log_i("PWM detached");
      // Reconfigure GPIO
      pinMode(PIN_CLK, OUTPUT);
      digitalWrite(PIN_CLK, LOW);
      clockState = false;
    }
  }
}
void  SLink::stopClock() {
  _clockInfo.Auto = false;
  setClock(_clockInfo);
}

void SLink::tick(bool fullCycle) {
  if(!_requireProgrammingDisabled("tick clock") || !_requirePowerOn("tick clock")) {
    return;
  }
  if(_clockInfo.Source != SLINK_CLOCK || _clockInfo.Auto) {
     log_e("Clock source must be S-Link with auto-tick disabled");
     return;
  }

  toogleClock();
  if(fullCycle && clockState) {
    delayMicroseconds((1000000UL / _clockInfo.Frequency) / 2);
    toogleClock();
  }
}

bool SLink::getProgrammerMode() {
  return _progrEnabled;
}
void SLink::setProgrammerMode(bool enable, bool resetOnExit) {
  if(_progrEnabled != enable) {
    if(!_requirePowerOn("change programming mode")) {
      return;
    }
    if(enable) {
      // Stop S-Link clock generation, then physically isolate any external
      // oscillator while preserving the user's configured clock source.
      stopClock();
      _mcp.setCtrl(CTRL_PIN_CLK_SRC, SLINK_CLOCK);
      pinMode(PIN_CLK, OUTPUT);
      digitalWrite(PIN_CLK, LOW);
      clockState = false;
      // Switch PROG MUX
      _mcp.setCtrl(CTRL_PIN_PROG_EN, HIGH);
      // Done
      log_i("Programmer mode enabled!");
    }
    else {
      if(resetOnExit) {
        // Keep the CPU in reset while returning its buses and clock source.
        _mcp.setCtrl(CTRL_PIN_RESET, LOW);
      }
      // Switch PROG MUX
      _mcp.setCtrl(CTRL_PIN_PROG_EN, LOW);
      // Restore the configured source only after the CPU owns its buses again.
      _mcp.setCtrl(CTRL_PIN_CLK_SRC, _clockInfo.Source);
      if(resetOnExit) {
        delay(MASTER_RESET_PULSE_MS);
        _mcp.setCtrl(CTRL_PIN_RESET, HIGH);
      }
      // Done
      log_i("Programmer mode disabled!");
    }
    _progrEnabled = enable;
  }
  else {
      log_e("Programming mode unchanged");
  }
}
