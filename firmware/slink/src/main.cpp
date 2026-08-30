#include <Arduino.h>

#include "pins.h"
#include "SPIFFS.h"
#include <WiFiManager.h>
#include <ESPmDNS.h>
#include <ArduinoJson.h>
#include "ESPAsyncWebServer.h"
#include "MCPManager.h"
#include "FlashProgrammer.h"
#include "SLink.h"

const String ROM_DIRECTORY = "/roms";
const String LAST_FLASH_INFO_FILE = "/last_flash.json";
const uint32_t ROM_WORD_CAPACITY = 0x10000;
const uint16_t RAM_WORD_CAPACITY = 2048;
const uint16_t RAM_MAX_ADDRESS = RAM_WORD_CAPACITY - 1;

const long TIMEZONE = 1;
const byte DAYSAVETIME = 1;

const unsigned long REPORT_INTERVAL_MS = 100;
const unsigned long SSE_CLIENT_TIMEOUT = 10000;

const uint8_t AUTO_RUN_RESULT_NONE = 0;
const uint8_t AUTO_RUN_RESULT_STARTED = 1;

const uint8_t FLASH_RESULT_NONE = 0;
const uint8_t FLASH_RESULT_SUCCESS = 1;
const uint8_t FLASH_RESULT_FAILED_PROGRAM_OR_ERASE = 2;
const uint8_t FLASH_RESULT_FAILED_VERIFY = 3;

WiFiManager wm;
AsyncWebServer server(80);
AsyncEventSource events("/events");
AsyncCorsMiddleware cors;

MCPManager mcp;
SLink slink(mcp);
FlashProgrammer programmer(mcp);

struct FlashJobInfo {
    bool active;
    String romFilename;
    size_t totalSize;
    size_t bytesWritten;
    unsigned long startTime;
    bool autoRunAfterFlash;
    uint8_t autoRunResult;
    uint8_t flashResult;
};
FlashJobInfo jobInfo;

struct LastFlashInfo {
    String filename;
    unsigned long timestamp;
};
LastFlashInfo lastFlashInfo;

void saveLastFlashInfo(const String& filename) {
  JsonDocument info;
  info["filename"] = filename;
  info["timestamp"] = (unsigned long)time(nullptr);

  File file = SPIFFS.open(LAST_FLASH_INFO_FILE, FILE_WRITE);
  if (!file) {
    log_w("Unable to persist last flash info");
    return;
  }

  serializeJson(info, file);
  file.close();
}

void loadLastFlashInfo() {
  lastFlashInfo = {};
  if (!SPIFFS.exists(LAST_FLASH_INFO_FILE)) {
    return;
  }

  File file = SPIFFS.open(LAST_FLASH_INFO_FILE, "r");
  if (!file) {
    return;
  }

  JsonDocument info;
  if (deserializeJson(info, file)) {
    file.close();
    return;
  }

  lastFlashInfo.filename = info["filename"] | "";
  lastFlashInfo.timestamp = info["timestamp"] | 0;
  file.close();
}

String getStatus() {
    // Create JSON
    JsonDocument info;
    info["state"] = slink.getPowerState();
    info["progmode"] = slink.getProgrammerMode();
    ClockInfo clock = slink.getClock();
    info["clock"]["source"] = clock.Source;
    info["clock"]["frequency"] = clock.Frequency;
    info["clock"]["auto"] = clock.Auto;
    if(jobInfo.active) {
      info["job"]["active"] = jobInfo.active;
      info["job"]["filename"] = jobInfo.romFilename;
      info["job"]["written"] = jobInfo.bytesWritten;
      info["job"]["totalSize"]= jobInfo.totalSize;
      unsigned long duration = millis() - jobInfo.startTime;
      info["job"]["duration"] = duration;
      info["job"]["speed"] = duration > 0 ? (float)jobInfo.bytesWritten / (duration / 1000.0) : 0;
    }
    else {
      info["job"] = nullptr;
    }
    if (lastFlashInfo.filename.length() > 0) {
      info["lastFlash"]["filename"] = lastFlashInfo.filename;
      info["lastFlash"]["timestamp"] = lastFlashInfo.timestamp;
    }
    // Return response
    String response;
    serializeJson(info, response);
    return response;
}

void reportRomsUpdated(String reason) {
  events.send(reason, "RomsUpdated", SSE_CLIENT_TIMEOUT);
}
void reportStateUpdated() {
  events.send(getStatus(), "StateUpdated", SSE_CLIENT_TIMEOUT);
}
void reportText(String text) {
  events.send(text, "Notify", SSE_CLIENT_TIMEOUT);
}

void reportStatusUpdate(String text) {
  events.send(text, "StatusUpdate", SSE_CLIENT_TIMEOUT);
}

void reportJob() {
    // Create JSON
  JsonDocument info;
  info["active"] = jobInfo.active;
  info["filename"] = jobInfo.romFilename;
  info["written"] = jobInfo.bytesWritten;
  info["totalSize"]= jobInfo.totalSize;
  unsigned long duration = millis() - jobInfo.startTime;
  info["duration"] = duration;
  info["speed"] = duration > 0 ? (float)jobInfo.bytesWritten / (duration / 1000.0) : 0;
  
  // If job finished, add completion details
  if (!jobInfo.active && jobInfo.bytesWritten > 0) {
    info["autoRunRequested"] = jobInfo.autoRunAfterFlash;
    info["autoRunResult"] = jobInfo.autoRunResult;
    info["flashResult"] = jobInfo.flashResult;
  }
  
  // Return response
  String response;
  serializeJson(info, response);
  events.send(response, "JobReport", millis());
}

bool parseWordValue(const String& token, uint16_t& value) {
  char* endPtr = nullptr;
  unsigned long parsed = strtoul(token.c_str(), &endPtr, 0);
  if (endPtr == token.c_str() || *endPtr != '\0' || parsed > 0xFFFFUL) {
    return false;
  }
  value = (uint16_t)parsed;
  return true;
}

bool parseWriteWords(const String& dataParam, uint16_t* outWords, size_t maxWords, size_t& outCount) {
  outCount = 0;
  String data = dataParam;
  data.trim();

  if (data.length() == 0) {
    outWords[0] = 1;
    outCount = 1;
    return true;
  }

  if (data.indexOf(',') >= 0) {
    int start = 0;
    while (start < data.length()) {
      int comma = data.indexOf(',', start);
      String token = (comma >= 0) ? data.substring(start, comma) : data.substring(start);
      token.trim();

      if (token.length() == 0 || outCount >= maxWords) {
        return false;
      }

      uint16_t value = 0;
      if (!parseWordValue(token, value)) {
        return false;
      }

      outWords[outCount++] = value;
      if (comma < 0) {
        break;
      }
      start = comma + 1;
    }
    return outCount > 0;
  }

  if (data.startsWith("0x") || data.startsWith("0X")) {
    data = data.substring(2);
  }

  if (data.length() <= 4) {
    uint16_t value = 0;
    if (!parseWordValue(String("0x") + data, value)) {
      return false;
    }
    outWords[0] = value;
    outCount = 1;
    return true;
  }

  if ((data.length() % 4) != 0) {
    return false;
  }

  size_t wordsToWrite = data.length() / 4;
  if (wordsToWrite == 0 || wordsToWrite > maxWords) {
    return false;
  }

  for (size_t i = 0; i < wordsToWrite; i++) {
    String chunk = data.substring(i * 4, (i + 1) * 4);
    uint16_t value = 0;
    if (!parseWordValue(String("0x") + chunk, value)) {
      return false;
    }
    outWords[outCount++] = value;
  }

  return true;
}

bool ensureProgrammingEnabled(AsyncWebServerRequest *request) {
  if(!slink.getProgrammerMode()) {
    request->send(400, "text/plain", "Programming mode is disabled");
    return false;
  }
  return true;
}

bool sendMissingParameter(AsyncWebServerRequest *request, const char* parameter) {
  request->send(400, "text/plain", String("Missing parameter: ") + parameter);
  return false;
}

bool sendMissingParameters(AsyncWebServerRequest *request, const char* parameters) {
  request->send(400, "text/plain", String("Missing parameters: ") + parameters);
  return false;
}

bool sendInvalidParameter(AsyncWebServerRequest *request, const char* parameter) {
  request->send(400, "text/plain", String("Invalid ") + parameter);
  return false;
}

bool ensurePowerOn(AsyncWebServerRequest *request, const char* action) {
  if(slink.getPowerState()) {
    return true;
  }
  request->send(400, "text/plain", String("Cannot ") + action + " while power is off");
  return false;
}

bool ensureProgrammingDisabled(AsyncWebServerRequest *request, const char* action) {
  if(!slink.getProgrammerMode()) {
    return true;
  }
  request->send(400, "text/plain", String("Cannot ") + action + " while programming mode is enabled");
  return false;
}

bool ensureNoActiveJob(AsyncWebServerRequest *request) {
  if(jobInfo.active) {
    request->send(409, "text/plain", "Job in progress");
    return false;
  }
  return true;
}

void onUpload(AsyncWebServerRequest *request, String filename, size_t index, uint8_t *data, size_t len, bool final) {
  if(!index){
    if(filename.length() == 0) {
      request->send(400, "text/plain", "Empty filename");
      return;
    }
    
    String fullPath = ROM_DIRECTORY + "/" + filename;
    
    // SPIFFS has a 31-character limit for full file paths
    if(fullPath.length() > 31) {
      int maxNameLen = 31 - ROM_DIRECTORY.length() - 1;
      request->send(400, "text/plain", "Filename too long (max " + String(maxNameLen) + " chars)");
      return;
    }
    
    log_i("Upload: %s", filename.c_str());
    request->_tempFile = SPIFFS.open(fullPath, "wb");
    if(!request->_tempFile) {
      size_t spiffsFree = SPIFFS.totalBytes() - SPIFFS.usedBytes();
      if (spiffsFree < (index + len)) {
        request->send(507, "text/plain", "Storage full");
      } else {
        request->send(500, "text/plain", "Write failed");
      }
      return;
    }
  }
  if (len) {
    if(request->_tempFile) {
      request->_tempFile.write(data, len);
    }
  }
  if (final) {
    if(request->_tempFile) {
      request->_tempFile.close();
      log_i("Upload complete: %s", filename.c_str());
      reportRomsUpdated("Upload");
      request->redirect("/");
    }
  }
}

void onDeleteImage(AsyncWebServerRequest *request) {
  if(!ensureNoActiveJob(request)) {
    return;
  }
  if(!request->hasParam("name")) {
    sendMissingParameter(request, "name");
    return;
  }

  String filename = request->getParam("name")->value();
  if(filename.length() == 0 || filename.indexOf('/') >= 0 || filename.indexOf('\\') >= 0) {
    sendInvalidParameter(request, "name");
    return;
  }

  String fullPath = ROM_DIRECTORY + "/" + filename;
  if(!SPIFFS.exists(fullPath)) {
    request->send(404, "text/plain", "File not found: " + filename);
    return;
  }

  log_i("Removing %s", fullPath.c_str());
  if(!SPIFFS.remove(fullPath)) {
    request->send(500, "text/plain", "Unable to delete file: " + filename);
    return;
  }

  reportRomsUpdated("Remove");
  request->send(204);
}

void setupHttpServer() {

  // Upload image to SPIFFS
  server.on("/images/upload", HTTP_POST, [](AsyncWebServerRequest *request) { }, onUpload);

  // POST /control/reset
  server.on("/control/reset", HTTP_POST, [](AsyncWebServerRequest *request) {
    if(!ensureProgrammingDisabled(request, "reset the S-CPU") || !ensurePowerOn(request, "reset")) {
      return;
    }
    slink.masterReset();
    request->send(200);
  });

  // POST /control/power?state=bool
  server.on("/control/power", HTTP_POST, [](AsyncWebServerRequest *request) {
    if(!ensureProgrammingDisabled(request, "change power state")) {
      return;
    }
    if(!request->hasParam("state")) {
      sendMissingParameter(request, "state");
      return;
    }

    bool state = request->getParam("state")->value() == "true";
    if(state) {
      // Turn ON
      slink.setPowerState(true);
      delay(10);
      slink.masterReset();
    }
    else {
      // Turn off
      slink.setPowerState(false);
    }
    request->send(200);
    reportStateUpdated();
  });
  
  // POST /control/clock
  server.on("/control/clock", HTTP_POST, [](AsyncWebServerRequest *request) {
    if(!ensureProgrammingDisabled(request, "change clock") || !ensurePowerOn(request, "change clock")) {
      return;
    }
    if(!request->hasParam("source") || !request->hasParam("frequency") || !request->hasParam("auto")) {
      sendMissingParameters(request, "source, frequency, auto");
      return;
    }

    ClockSource source = (ClockSource)request->getParam("source")->value().toInt();
    long frequency = request->getParam("frequency")->value().toInt();
    bool autoMode = request->getParam("auto")->value() == "true";
    if(source == NE555_CLOCK || (source == SLINK_CLOCK && frequency >= 0 && frequency <= MAX_FREQUENCY)) {
      slink.setClock({ source, frequency, autoMode });
      request->send(200);
      reportStateUpdated();
    }
    else {
      sendInvalidParameter(request, "clock parameters");
    }
  });
  
  // POST /control/tick
  server.on("/control/tick", HTTP_POST, [](AsyncWebServerRequest *request) {
    if(!ensureProgrammingDisabled(request, "tick clock") || !ensurePowerOn(request, "tick clock")) {
      return;
    }
    ClockInfo clock = slink.getClock();
    if(clock.Source != SLINK_CLOCK || clock.Auto) {
      request->send(400, "text/plain", "Clock source must be S-Link and auto-tick disabled");
      return;
    }
    if(!request->hasParam("full")) {
      sendMissingParameter(request, "full");
      return;
    }

    bool fullCycle = request->getParam("full")->value() == "true";
    slink.tick(fullCycle);
    request->send(200);
    reportStateUpdated();
  });

  // POST /programming/state?state=bool
  server.on("/programming/state", HTTP_POST, [](AsyncWebServerRequest *request) {
    if(!ensureNoActiveJob(request)) {
      return;
    }
    if(!ensurePowerOn(request, "change programming mode")) {
      return;
    }
    if(!request->hasParam("state")) {
      sendMissingParameter(request, "state");
      return;
    }

    bool state = request->getParam("state")->value() == "true";
    if(state) {
      // Isolate the CPU first, then let the programmer drive the buses.
      slink.setProgrammerMode(true);
      programmer.setState(true, true);
    }
    else {
      // Release the data bus before reconnecting the CPU and its clock source.
      programmer.setState(false);
      slink.setProgrammerMode(false);
    }
    request->send(200);
    reportStateUpdated();
  });

  // GET /rom/read?address=int&count=int(optional, default=1)
  server.on("/rom/read", HTTP_GET, [](AsyncWebServerRequest *request) {
    if(!ensureProgrammingEnabled(request)) {
      return;
    }
    if(!ensureNoActiveJob(request)) {
      return;
    }
    if(!request->hasParam("address")) {
      sendMissingParameter(request, "address");
      return;
    }

    int address = request->getParam("address")->value().toInt();
    bool hasCount = request->hasParam("count");
    int count = hasCount ? request->getParam("count")->value().toInt() : 1;
    if(address < 0 || address > 0xFFFF) {
      sendInvalidParameter(request, "address");
    }
    else if(count <= 0 || count > 2048) {
      sendInvalidParameter(request, "ROM count");
    }
    else if((address + count) > (int)ROM_WORD_CAPACITY) {
      request->send(400, "text/plain", "ROM range out of bounds");
    }
    else {
      programmer.setState(true, false);  // Enter READ mode
      JsonDocument response;
      response["address"] = address;
      if(hasCount) {
        response["count"] = count;
        JsonArray data = response["data"].to<JsonArray>();
        for(int i = 0; i < count; i++) {
          data.add(programmer.readRom((uint16_t)(address + i)));
        }
      }
      else {
        // Preserve the historical single-word response for existing clients.
        response["data"] = programmer.readRom((uint16_t)address);
      }
      String jsonStr;
      serializeJson(response, jsonStr);
      request->send(200, "application/json", jsonStr);
    } 
  });

  // GET /rom/dump.bin (full 64K words, big-endian byte order)
  server.on("/rom/dump.bin", HTTP_GET, [](AsyncWebServerRequest *request) {
    if(!ensureProgrammingEnabled(request)) {
      return;
    }
    if(!ensureNoActiveJob(request)) {
      return;
    }

    programmer.setState(true, false);
    const size_t dumpSize = ROM_WORD_CAPACITY * 2;
    AsyncWebServerResponse *response = request->beginResponse(
      "application/octet-stream",
      dumpSize,
      [](uint8_t *buffer, size_t maxLen, size_t index) -> size_t {
        size_t remaining = (ROM_WORD_CAPACITY * 2) - index;
        size_t outputSize = min(maxLen, remaining);
        size_t outputIndex = 0;

        while(outputIndex < outputSize) {
          size_t byteOffset = index + outputIndex;
          uint16_t word = programmer.readRom((uint16_t)(byteOffset / 2));
          if((byteOffset & 1) == 0) {
            buffer[outputIndex++] = (uint8_t)(word >> 8);
            if(outputIndex < outputSize) {
              buffer[outputIndex++] = (uint8_t)(word & 0xFF);
            }
          }
          else {
            buffer[outputIndex++] = (uint8_t)(word & 0xFF);
          }
        }
        return outputSize;
      }
    );
    response->addHeader("Content-Disposition", "attachment; filename=rom_dump_64k.bin");
    request->send(response);
  });

  // POST /rom/write?address=int&data=int
  server.on("/rom/write", HTTP_POST, [](AsyncWebServerRequest *request) {
    if(!ensureProgrammingEnabled(request)) {
      return;
    }
    if(!ensureNoActiveJob(request)) {
      return;
    }
    if(!request->hasParam("address") || !request->hasParam("data")) {
      sendMissingParameters(request, "address, data");
      return;
    }

    int data = request->getParam("data")->value().toInt();
    int address = request->getParam("address")->value().toInt();
    if(address < 0 || address > 0xFFFF) {
      sendInvalidParameter(request, "address");
    }
    else if(data < 0 || data > 0xFFFF) {
      sendInvalidParameter(request, "data");
    } 
    else {
      programmer.setState(true, true);
      if(!programmer.programData(address, data)) {
        request->send(500, "text/plain", "ROM write failed");
        return;
      }
      request->send(200);
    } 
  });

  // POST /rom/erase
  server.on("/rom/erase", HTTP_POST, [](AsyncWebServerRequest *request) {
    if(!ensureProgrammingEnabled(request)) {
      return;
    }
    if(!ensureNoActiveJob(request)) {
      return;
    }

    programmer.setState(true, true);
    jobInfo = { true, "Erase all", 0, 0, 0, false, AUTO_RUN_RESULT_NONE, FLASH_RESULT_NONE };
    request->send(200);
  });

  // GET /ram/read?address=int&count=int(optional, default=1)
  server.on("/ram/read", HTTP_GET, [](AsyncWebServerRequest *request) {
    if(!ensureProgrammingEnabled(request)) {
      return;
    }
    if(!ensureNoActiveJob(request)) {
      return;
    }
    if(!request->hasParam("address")) {
      sendMissingParameter(request, "address");
      return;
    }

    int address = request->getParam("address")->value().toInt();
    int count = request->hasParam("count") ? request->getParam("count")->value().toInt() : 1;
    if(address < 0 || address > RAM_MAX_ADDRESS) {
      sendInvalidParameter(request, "RAM address");
      return;
    }
    if(count <= 0) {
      sendInvalidParameter(request, "RAM count");
      return;
    }
    if((address + count) > RAM_WORD_CAPACITY) {
      request->send(400, "text/plain", "RAM range out of bounds");
      return;
    }

    programmer.setState(true, false);
    JsonDocument response;
    response["address"] = address;
    response["count"] = count;
    JsonArray data = response["data"].to<JsonArray>();
    for(int i = 0; i < count; i++) {
      data.add(programmer.readRam((uint16_t)(address + i)));
    }

    String json;
    serializeJson(response, json);
    request->send(200, "application/json", json);
  });

  // POST /ram/write?address=int&data=... (default data=1)
  // data formats:
  // - single word: data=4660 or data=0x1234
  // - multiple words CSV: data=0x1234,0xABCD,0x0001
  // - packed hex words: data=1234ABCD0001
  server.on("/ram/write", HTTP_POST, [](AsyncWebServerRequest *request) {
    if(!ensureProgrammingEnabled(request)) {
      return;
    }
    if(!ensureNoActiveJob(request)) {
      return;
    }
    if(!request->hasParam("address")) {
      sendMissingParameter(request, "address");
      return;
    }

    int address = request->getParam("address")->value().toInt();
    if(address < 0 || address > RAM_MAX_ADDRESS) {
      sendInvalidParameter(request, "RAM address");
      return;
    }

    String dataParam = request->hasParam("data") ? request->getParam("data")->value() : "";
    uint16_t words[256];
    size_t wordCount = 0;
    if(!parseWriteWords(dataParam, words, 256, wordCount)) {
      sendInvalidParameter(request, "data format");
      return;
    }
    if((address + (int)wordCount) > RAM_WORD_CAPACITY) {
      request->send(400, "text/plain", "RAM write out of bounds");
      return;
    }

    programmer.setState(true, true);
    for(size_t i = 0; i < wordCount; i++) {
      if(!programmer.writeRam((uint16_t)(address + i), words[i])) {
        request->send(500, "text/plain", "RAM write failed");
        return;
      }
    }

    JsonDocument response;
    response["address"] = address;
    response["written"] = wordCount;
    response["nextAddress"] = address + (int)wordCount;
    String json;
    serializeJson(response, json);
    request->send(200, "application/json", json);
  });

  // POST /ram/fill?value=0|0xFFFF (default 0)
  server.on("/ram/fill", HTTP_POST, [](AsyncWebServerRequest *request) {
    if(!ensureProgrammingEnabled(request)) {
      return;
    }
    if(!ensureNoActiveJob(request)) {
      return;
    }

    String valueParam = request->hasParam("value") ? request->getParam("value")->value() : "0";
    uint16_t fillValue = 0;
    if(!parseWordValue(valueParam, fillValue)) {
      sendInvalidParameter(request, "fill value");
      return;
    }
    if(fillValue != 0x0000 && fillValue != 0xFFFF) {
      request->send(400, "text/plain", "Fill value must be 0x0000 or 0xFFFF");
      return;
    }

    programmer.setState(true, true);
    for(uint16_t addr = 0; addr < RAM_WORD_CAPACITY; addr++) {
      if(!programmer.writeRam(addr, fillValue)) {
        request->send(500, "text/plain", "RAM fill failed");
        return;
      }
    }

    JsonDocument response;
    response["count"] = RAM_WORD_CAPACITY;
    response["value"] = fillValue;
    String json;
    serializeJson(response, json);
    request->send(200, "application/json", json);
  });

  // GET /ram/dump.bin (full 2K words, big-endian word order)
  server.on("/ram/dump.bin", HTTP_GET, [](AsyncWebServerRequest *request) {
    if(!ensureProgrammingEnabled(request)) {
      return;
    }
    if(!ensureNoActiveJob(request)) {
      return;
    }

    programmer.setState(true, false);
    AsyncResponseStream *response = request->beginResponseStream("application/octet-stream");
    response->addHeader("Content-Disposition", "attachment; filename=ram_dump_2k.bin");
    for(uint16_t addr = 0; addr < RAM_WORD_CAPACITY; addr++) {
      uint16_t word = programmer.readRam(addr);
      uint8_t bytes[2] = {
        (uint8_t)(word >> 8),
        (uint8_t)(word & 0xFF)
      };
      response->write(bytes, 2);
    }
    request->send(response);
  });

  // GET /ram/dump (full 2K words)
  server.on("/ram/dump", HTTP_GET, [](AsyncWebServerRequest *request) {
    if(!ensureProgrammingEnabled(request)) {
      return;
    }
    if(!ensureNoActiveJob(request)) {
      return;
    }

    programmer.setState(true, false);
    JsonDocument response;
    response["address"] = 0;
    response["count"] = RAM_WORD_CAPACITY;
    JsonArray data = response["data"].to<JsonArray>();
    for(uint16_t addr = 0; addr < RAM_WORD_CAPACITY; addr++) {
      data.add(programmer.readRam(addr));
    }

    String json;
    serializeJson(response, json);
    request->send(200, "application/json", json);
  });

  // POST /ram/upload?file=name&address=int(optional, default=0)
  // Uploads a SPIFFS file into RAM (word stream).
  server.on("/ram/upload", HTTP_POST, [](AsyncWebServerRequest *request) {
    if(!ensureProgrammingEnabled(request)) {
      return;
    }
    if(!ensureNoActiveJob(request)) {
      return;
    }
    if(!request->hasParam("file")) {
      sendMissingParameter(request, "file");
      return;
    }

    int startAddress = request->hasParam("address") ? request->getParam("address")->value().toInt() : 0;
    if(startAddress < 0 || startAddress > RAM_MAX_ADDRESS) {
      sendInvalidParameter(request, "RAM address");
      return;
    }

    String filename = request->getParam("file")->value();
    File file = SPIFFS.open(ROM_DIRECTORY + "/" + filename);
    if(!file) {
      request->send(404, "text/plain", "File not found");
      return;
    }

    size_t bytesAvailable = file.size();
    size_t wordsToWrite = (bytesAvailable + 1) / 2;
    if((startAddress + (int)wordsToWrite) > RAM_WORD_CAPACITY) {
      file.close();
      request->send(400, "text/plain", "File too large for RAM range");
      return;
    }

    programmer.setState(true, true);
    uint16_t address = (uint16_t)startAddress;
    while(file.available()) {
      uint8_t msb = (uint8_t)file.read();
      uint8_t lsb = file.available() ? (uint8_t)file.read() : 0;
      uint16_t word = ((uint16_t)msb << 8) | lsb;
      if(!programmer.writeRam(address++, word)) {
        file.close();
        request->send(500, "text/plain", "RAM upload failed");
        return;
      }
    }
    file.close();

    JsonDocument response;
    response["file"] = filename;
    response["startAddress"] = startAddress;
    response["writtenWords"] = wordsToWrite;
    response["nextAddress"] = startAddress + (int)wordsToWrite;
    String json;
    serializeJson(response, json);
    request->send(200, "application/json", json);
  });

  // GET /images (list images)
  server.on("/images", HTTP_GET, [](AsyncWebServerRequest *request) {
    JsonDocument doc;
    JsonArray files = doc["files"].to<JsonArray>();
    File root = SPIFFS.open(ROM_DIRECTORY);
    File foundfile = root.openNextFile();
    while (foundfile) {
      JsonDocument file;
      file["path"] = foundfile.name();
      file["size"] = foundfile.size();
      file["lastwrite"] = foundfile.getLastWrite();
      files.add(file);
      foundfile = root.openNextFile();
    }
    String response;
    serializeJson(doc, response);
    request->send(200, "application/json", response);
  });

  // DELETE /images/file?name=filename
  server.on("/images/file", AsyncWebRequestMethod::HTTP_DELETE, onDeleteImage);

  // POST /images/rename?file=old&newName=new
  server.on("/images/rename", HTTP_POST, [](AsyncWebServerRequest *request) {
    if(!ensureNoActiveJob(request)) {
      return;
    }
    if(!request->hasParam("file") || !request->hasParam("newName")) {
      sendMissingParameters(request, "file, newName");
      return;
    }

    String filename = request->getParam("file")->value();
    String newName = request->getParam("newName")->value();
    if(newName.length() == 0) {
      request->send(400, "text/plain", "Empty filename");
      return;
    }

    String fullPath = ROM_DIRECTORY + "/" + newName;
    if(fullPath.length() > 31) {
      int maxNameLen = 31 - ROM_DIRECTORY.length() - 1;
      request->send(400, "text/plain", "Filename too long (max " + String(maxNameLen) + " chars)");
      return;
    }

    File file = SPIFFS.open(ROM_DIRECTORY + "/" + filename);
    if(!file || file.size() == 0) {
      request->send(404, "text/plain", "File not found");
      return;
    }

    log_i("Renaming %s to %s", file.name(), newName.c_str());
    File destFile = SPIFFS.open(fullPath, FILE_WRITE);
    if (!destFile) {
      file.close();
      request->send(400, "text/plain", "Unable to create the new file");
      return;
    }

    while (file.available()) {
      destFile.write(file.read());
    }
    destFile.close();
    SPIFFS.remove(file.path());
    file.close();
    reportRomsUpdated("Rename");
    request->send(204);
  });

  // POST /images/flash?file=name&autoRun=bool(optional)
  server.on("/images/flash", HTTP_POST, [](AsyncWebServerRequest *request) {
    if(!ensureNoActiveJob(request)) {
      return;
    }
    if(!request->hasParam("file")) {
      sendMissingParameter(request, "file");
      return;
    }
    if(!slink.getProgrammerMode()) {
      request->send(400, "text/plain", "Programming mode is disabled");
      return;
    }
    if(!slink.getPowerState()) {
      request->send(400, "text/plain", "Power is off");
      return;
    }

    bool autoRunAfterFlash = request->hasParam("autoRun") && request->getParam("autoRun")->value() == "true";

    String filename = request->getParam("file")->value();
    File file = SPIFFS.open(ROM_DIRECTORY + "/" + filename);
    if(!file || file.size() == 0) {
      request->send(404, "text/plain", "File not found");
      return;
    }

    log_i("Flashing %s", file.name());
    jobInfo = { true, file.name(), file.size(), 0, 0, autoRunAfterFlash, AUTO_RUN_RESULT_NONE, FLASH_RESULT_NONE };
    file.close();
    request->send(204);
  });

  // GET /status
  server.on("/status", HTTP_GET, [](AsyncWebServerRequest *request) {
    request->send(200, "application/json", getStatus());
  });

  // GET /sysinfo
  server.on("/sysinfo", HTTP_GET, [](AsyncWebServerRequest *request) {
    // Get time
    struct tm tmstruct;
    getLocalTime(&tmstruct);
    time_t now;
    time(&now);
    // Create JSON
    JsonDocument info;
    info["time"] = now;
    info["model"]= ESP.getChipModel();
    info["heap"]["free"] = ESP.getFreeHeap();
    info["heap"]["total"] = ESP.getHeapSize();
    info["SPIFFS"]["total"]= SPIFFS.totalBytes();
    info["SPIFFS"]["used"]= SPIFFS.usedBytes();
    info["SPIFFS"]["free"]= SPIFFS.totalBytes() - SPIFFS.usedBytes();
    info["network"]["SSID"]= WiFi.SSID();
    info["network"]["status"]= WiFi.status();
    info["network"]["RSSI"]= WiFi.RSSI();
    info["network"]["localIP"]= WiFi.localIP();
    info["network"]["macAddress"]= WiFi.macAddress();
    info["network"]["gatewayIP"]= WiFi.gatewayIP();
    // Return response
    String response;
    serializeJson(info, response);
    request->send(200, "application/json", response);
  });

  // Serve static files
  server.serveStatic("/", SPIFFS, "/").setDefaultFile("index.html");

  // GET /favicon.ico
  server.on("/favicon.ico", HTTP_GET, [](AsyncWebServerRequest *request) {
    if (SPIFFS.exists("/favicon.ico")) {
      AsyncWebServerResponse *response = request->beginResponse(SPIFFS, "/favicon.ico", "image/x-icon");
      response->addHeader("Cache-Control", "public, max-age=86400");
      request->send(response);
    } else {
      request->send(404, "text/plain", "Not found");
    }
  });

  // 404 not found route
  server.onNotFound([] (AsyncWebServerRequest *request) {
    request->send(404, "text/plain", "Not found");
  });

  // Adds CORS middleware
  cors.setOrigin("*");
  cors.setMethods("POST, GET, OPTIONS, DELETE");
  cors.setAllowCredentials(false);
  cors.setMaxAge(600);
  server.addMiddleware(&cors);

  // Adds EventSource
  server.addHandler(&events);

  // Start HTTP server
  server.begin();
}

void setup() {
  // Initialize serial monitor for debugging
  #ifdef DEBUG_SERIAL
    Serial.begin(115200);
    delay(3000);
    log_i("Serial interface initialized");
  #endif

  // Initialize SPIFFS
  log_i("Mounting SPIFFS");
  if(!SPIFFS.begin(true)){
    log_e("SPIFFS mount failed");
    return;
  }
  loadLastFlashInfo();
  
  // Warn if storage space is low
  size_t spiffsFree = SPIFFS.totalBytes() - SPIFFS.usedBytes();
  if (spiffsFree < 50000) {
    log_w("WARNING: SPIFFS space low (%d bytes free)", spiffsFree);
  }

  log_i("Starting MCP manager");
  // Initialize MCP23S17 Manager
  if(!mcp.begin()) {
    log_e("Unable to initialize MCP manager!");
    return;
  }

  // Initialize S-Link
  log_i("Starting SLink manager");
  if(!slink.begin()) {
    log_e("Unable to initialize SLink manager!");
    return;
  }

  // Initialize flash programmer
  log_i("Starting Flash programmer");
  if(!programmer.begin()) {
    log_e("Unable to initialize Flash programmer!");
    return;
  }

  // Initialize WiFi
  log_i("Starting WiFi");
  WiFi.mode(WIFI_STA);
  WiFi.setSleep(false);
  delay(1000);
  if(!wm.autoConnect("SLink")) {
    log_e("Failed to connect to WiFi");
    ESP.restart();
    return;
  }

  // Initialize mDNS
  #ifdef MDNS_NAME
    log_i("Starting mDNS");
    if (!MDNS.begin(MDNS_NAME)) {
      log_e("Unable to initialize MDNS responder");
      return;
    }
  #else
    log_w("MDNS_NAME not defined, skipping mDNS initialization");
  #endif

  // Configure time via NTP
  log_i("Configuring time");
  configTime(3600 * TIMEZONE, DAYSAVETIME * 3600, "time.nist.gov", "0.pool.ntp.org", "1.pool.ntp.org");

  // Start HTTP server
  log_i("Starting HTTP server");
  setupHttpServer();

  // Done
  log_i("SLink started & connected");
}

void loop() {

  // If job exists
  if(jobInfo.active) {
    // Erase flash
    if(jobInfo.totalSize == 0) {
        if(slink.getProgrammerMode() && programmer.getState()) {
          log_i("Erasing flash ...");
          if(programmer.eraseChip()) {
            reportText("Erasing ROM completed!");
          }
          else {
            reportText("ROM erase failed");
          }
        }
        else {
          log_e("Invalid state for ROM erase");
        }
        jobInfo.active = false;
        reportJob();
    }
    else {
      log_i("Starting flash of %s", jobInfo.romFilename);
      jobInfo.startTime = millis();  // Track start time for speed calculation
      reportJob();
      
      // Open ROM file
      File file = SPIFFS.open(ROM_DIRECTORY + "/" + jobInfo.romFilename);
      if(!file){
        log_e("File not found: %s", jobInfo.romFilename);
        jobInfo.active = false;
        reportText("File not found");
        reportJob();
        return;
      }
      
      // ========== PHASE 1: ERASE ==========
      slink.setProgrammerMode(true);
      programmer.setState(true, true);  // Enable WRITE mode
      
      reportStatusUpdate("Erasing ROM...");
      bool flashFailed = false;
      if(!programmer.eraseChip()) {
        flashFailed = true;
      }
      if(!flashFailed) {
        delay(100);
      }
      
      // ========== PHASE 2: PROGRAM ==========
      if(!flashFailed) {
        reportStatusUpdate("Programming ROM...");
      }
      
      size_t verifySize = (jobInfo.totalSize % 2 == 0) ? jobInfo.totalSize : (jobInfo.totalSize + 1);
      uint8_t* verifyBuffer = nullptr;
      if(!flashFailed) {
        // Allocate verify buffer (word-aligned for odd-sized ROM images)
        verifyBuffer = new uint8_t[verifySize];
        if (!verifyBuffer) {
          log_e("Out of memory");
          reportText("Out of memory");
          file.close();
          programmer.setState(false);
          slink.setProgrammerMode(false);
          jobInfo.active = false;
          reportJob();
          return;
        }
      }
      
      // Program flash and store in verify buffer
      uint16_t address = 0;
      uint16_t bufferIndex = 0;
      unsigned long lastReportTime = millis();
      
      while(!flashFailed && file.available() && bufferIndex < verifySize) {
        uint16_t msb = file.read();
        bool hasLsb = file.available();
        uint16_t lsb = hasLsb ? file.read() : 0;
        uint16_t data = (uint16_t)msb << 8 | lsb;
        
        // Store in verify buffer
        verifyBuffer[bufferIndex++] = lsb;  // LSB first
        if(bufferIndex < verifySize) {
          verifyBuffer[bufferIndex++] = msb;  // MSB second
        }
        
        jobInfo.bytesWritten += hasLsb ? 2 : 1;
        if(!programmer.programData(address++, data)) {
          flashFailed = true;
          break;
        }
        
        // Periodic report
        unsigned long currentTime = millis();
        if (currentTime - lastReportTime >= REPORT_INTERVAL_MS) {
          reportJob();
          lastReportTime = currentTime;
        }
      }
      
      file.close();
      if(!flashFailed) {
        reportStatusUpdate("Verifying ROM...");
      }
      
      // ========== PHASE 3: VERIFY ==========
      bool verifyPassed = false;
      if(!flashFailed) {
        programmer.setState(true, false);  // Enable READ mode
        verifyPassed = programmer.verifyRom(verifyBuffer, verifySize);
      }
      
      if (verifyPassed) {
        jobInfo.flashResult = FLASH_RESULT_SUCCESS;
        log_i("Flash complete: %s - PASSED", jobInfo.romFilename.c_str());
        lastFlashInfo.filename = jobInfo.romFilename;
        lastFlashInfo.timestamp = (unsigned long)time(nullptr);
        saveLastFlashInfo(jobInfo.romFilename);
        reportText("Flash PASSED");
      } else {
        if(flashFailed) {
          jobInfo.flashResult = FLASH_RESULT_FAILED_PROGRAM_OR_ERASE;
          log_e("Flash failed: %s - Program or erase operation failed", jobInfo.romFilename.c_str());
          reportText("Flash FAILED");
        }
        else {
          jobInfo.flashResult = FLASH_RESULT_FAILED_VERIFY;
          log_e("Flash failed: %s - Verification mismatch", jobInfo.romFilename.c_str());
          reportText("Flash FAILED");
        }
      }
      
      delete[] verifyBuffer;
      
      if(verifyPassed && jobInfo.autoRunAfterFlash) {
        programmer.setState(false);
        slink.setProgrammerMode(false, true);
        ClockInfo clock = slink.getClock();
        if(clock.Source == SLINK_CLOCK) {
          clock.Auto = true;
          slink.setClock(clock);
        }
        // NE555 resumes when RESET is released; S-Link starts above.
        jobInfo.autoRunResult = AUTO_RUN_RESULT_STARTED;
      }
      else if(jobInfo.autoRunAfterFlash) {
        // Never resume the CPU against a partially programmed or unverified ROM.
        // Keep the clock isolated and leave read access available for inspection.
        programmer.setState(true, false);
        slink.setProgrammerMode(true);
      }
      else {
        // Keep programming mode enabled for inspection or another operation.
        programmer.setState(true, false);
        slink.setProgrammerMode(true);
      }

      jobInfo.active = false;
      reportJob();
      reportStateUpdated();
    }
  }

}
