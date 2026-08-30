#ifndef pins_h
#define pins_h

#if defined(BOARD_ID) && BOARD_ID == ESP32_C3

#define PIN_CS   SS
#define PIN_SCK  SCK
#define PIN_MOSI MOSI
#define PIN_MISO MISO

#define PIN_CLK  8

#elif defined(BOARD_ID) && BOARD_ID == ESP32_S3

#define PIN_CS   21
#define PIN_SCK  48
#define PIN_MOSI 38
#define PIN_MISO 47

#define PIN_CLK  18

#endif

#endif