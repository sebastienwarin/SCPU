#include "pins.h"
#include "SLink.h"

bool clockState = false;
bool isPwmActive = false;
void IRAM_ATTR toogleClock() {
  clockState = !clockState;
  digitalWrite(CLK, clockState ? HIGH : LOW);
}

SLink::SLink(uint8_t address) {
  // Init PCF8574
  _pcf = new PCF8574(address);
}

bool SLink::begin() {
  // Init GPIO
  pinMode(CLK, OUTPUT);
  digitalWrite(CLK, LOW);
  // Init PCF8574 I/O
  for (uint8_t i = 0; i < 8; i++) {
    _pcf->pinMode(i, OUTPUT, i == PSU_RELAY ? HIGH : LOW);
  }
  return _pcf->begin();
}

bool SLink::getPowerState() {
  return _powerState;
}
void SLink::setPowerState(bool state) {
  if(_progrEnabled) {
    log_e("Cannot change the power state while the programmation mode is enabled !");
  }
  else if(state) {
    // Switch relay on
    _pcf->digitalWrite(PSU_RELAY, LOW);
    _powerState = true;
    // Set GPIO to initial state
    _pcf->digitalWrite(CLK_SRC, HIGH);
    _pcf->digitalWrite(RESET, HIGH);
    _pcf->digitalWrite(PROG_EN, LOW);
  }
  else {
    // Stop clock
    stopClock();
    // Turn off GPIO
    _pcf->digitalWrite(PROG_EN, LOW);
    _pcf->digitalWrite(RESET, LOW);
    _pcf->digitalWrite(CLK_SRC, LOW);
    // Switch off relay
    _pcf->digitalWrite(PSU_RELAY, HIGH);
    _powerState = false;
  }
}

void SLink::masterReset() {
  if(_progrEnabled) {
    log_e("Cannot reset the SCPU while the programmation mode is enabled !");
  }
  else if(!_powerState) {
     log_e("Cannot reset while the power state is off");
  }
  else {
    _pcf->digitalWrite(RESET, LOW);
    delay(MASTER_RESET_PULSE_MS);
    _pcf->digitalWrite(RESET, HIGH);
  }
}

ClockInfo SLink::getClock() {
  return _clockInfo;
}
void SLink::setClock(ClockInfo clockInfo) {
  if(_progrEnabled) {
    log_e("Cannot change the clock while the programmation mode is enabled !");
  }
  else if(!_powerState) {
     log_e("Cannot change the clock while the power state is off");
  }
  else {
    _clockInfo = clockInfo;
    // Clock source
    _pcf->digitalWrite(CLK_SRC, clockInfo.Source);
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
      ledcAttachPin(CLK, PWM_CHANNEL);
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
        ledcDetachPin(CLK);
        isPwmActive = false;
        log_i("PWM detached");
        // Reconfigure GPIO
        pinMode(CLK, OUTPUT);
        digitalWrite(CLK, LOW);
        clockState = false;
      }
    }
  }
}
void  SLink::stopClock() {
  _clockInfo.Auto = false;
  setClock(_clockInfo);
}

void SLink::tick(bool fullCycle) {
  if(_progrEnabled) {
    log_e("Cannot tick the clock while the programmation mode is enabled !");
  }
  else if(!_powerState) {
     log_e("Cannot tick the clock while the power state is off");
  }
  else if(_clockInfo.Source != SLINK_CLOCK || _clockInfo.Auto) {
     log_e("The clock source must be SLink and auto-tick disabled");
  }
  else {
    toogleClock();
    if(fullCycle && clockState) {
      delayMicroseconds((1000000UL / _clockInfo.Frequency) / 2);
      toogleClock();
    }
  }
}

bool SLink::getProgrammerMode() {
  return _progrEnabled;
}
void SLink::setProgrammerMode(bool enable) {
  if(_progrEnabled != enable) {
    if(!_powerState) {
     log_e("Cannot change the mode while the power state is off");
    }
    else if(enable) {
      // Stop clock
      stopClock();
      // Reset to force S0
      masterReset();
      // Switch PROG MUX
      _pcf->digitalWrite(PROG_EN, HIGH);
      // Done
      log_i("Programmer mode enabled!");
    }
    else {
      // Switch PROG MUX
      _pcf->digitalWrite(PROG_EN, LOW);
      // Done
      log_i("Programmer mode disabled!");
    }
    _progrEnabled = enable;
  }
  else {
     log_e("setProgrammerMode unchanged!");
  }
}