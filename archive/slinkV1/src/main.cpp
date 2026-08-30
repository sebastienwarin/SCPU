#include <Arduino.h>

#include "pins.h"
#include "SPIFFS.h"
#include <WiFiManager.h>
#include <ESPmDNS.h>
#include <ArduinoJson.h>
#include "ESPAsyncWebServer.h"
#include "FlashProgrammer.h"
#include "SLink.h"

#define I2C_FREQUENCY 400000
#define SSE_CLIENT_TIMEOUT 10000

const String ROM_DIRECTORY = "/roms";
const long timezone = 1;
const byte daysavetime = 1;

WiFiManager wm;
AsyncWebServer server(80);
AsyncEventSource events("/events");
AsyncCorsMiddleware cors;

SLink slink(PCF_MST);
FlashProgrammer programmer(PCF_ROM);

struct FlashJobInfo {
    bool active;
    String romFilename;
    size_t totalSize;
    size_t bytesWritten;
};
FlashJobInfo jobInfo;

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
    }
    else {
      info["job"] = nullptr;
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

void reportJob() {
    // Create JSON
  JsonDocument info;
  info["active"] = jobInfo.active;
  info["filename"] = jobInfo.romFilename;
  info["written"] = jobInfo.bytesWritten;
  info["totalSize"]= jobInfo.totalSize;
  // Return response
  String response;
  serializeJson(info, response);
  events.send(response, "JobReport", millis());
}

void onUpload(AsyncWebServerRequest *request, String filename, size_t index, uint8_t *data, size_t len, bool final) {
  if(!index){
    log_i("Starting upload: %s", filename);
    request->_tempFile = SPIFFS.open(ROM_DIRECTORY + "/" + filename, "wb");
  }
  if (len) {
    log_d("Writing file: %s (%d/%d)", filename, index, len);
    request->_tempFile.write(data, len);
  }
  if (final) {
    request->_tempFile.close();
    log_i("Upload Complete: %s (size:%d)", filename, (index + len));
    reportRomsUpdated("Upload");
    request->redirect("/");
  }
}

void setupHttpServer() {

  // Upload ROM
  server.on("/upload", HTTP_POST, [](AsyncWebServerRequest *request) { }, onUpload);

  // GET /control/reset
  server.on("/control/reset", HTTP_GET, [](AsyncWebServerRequest *request) {
    if(slink.getProgrammerMode()) {
      request->send(400, "text/plain", "Cannot reset the SCPU while the programmation mode is enabled !"); 
    }
    else if(!slink.getPowerState()) {
      request->send(400, "text/plain", "Cannot reset while the power state is off !"); 
    }
    else {
      slink.masterReset();
      request->send(200);
    }
  });

  // GET /control/power?state=bool
  server.on("/control/power", HTTP_GET, [](AsyncWebServerRequest *request) {
    if(slink.getProgrammerMode()) {
      request->send(400, "text/plain", "Cannot change the power state while the programmation mode is enabled !");      
    }
    else if(request->hasParam("state")) {
      bool state = request->getParam("state")->value() == "true";
      if(state) {
        // Turn ON
        slink.setPowerState(true);
        programmer.setOutput(true);
        delay(10);
        slink.masterReset();
      }
      else {
        // Turn off
        programmer.setOutput(false);
        slink.setPowerState(false);
      }
      request->send(200);
      reportStateUpdated();
    }
    else {
      request->send(400, "text/plain", "Invalid parameters");
    }
  });
  
  // GET /control/clock
  server.on("/control/clock", HTTP_GET, [](AsyncWebServerRequest *request) {
    if(slink.getProgrammerMode()) {
      request->send(400, "text/plain", "Cannot change the clock while the programmation mode is enabled !"); 
    }
    else if(!slink.getPowerState()) {
      request->send(400, "text/plain", "Cannot change the clock while the power state is off !"); 
    } 
    else {
      ClockSource source = (ClockSource)request->getParam("source")->value().toInt();
      long frequency = request->getParam("frequency")->value().toInt();
      bool autoMode = request->getParam("auto")->value() == "true";
      if(source == NE555_CLOCK || (source == SLINK_CLOCK && frequency >= 0 && frequency <= MAX_FREQUENCY)) {
        slink.setClock({ source, frequency, autoMode });
        request->send(200);
        reportStateUpdated();
      }
      else {
        request->send(400, "text/plain", "Invalid parameters");
      }
    }
  });
  
  // GET /control/tick
  server.on("/control/tick", HTTP_GET, [](AsyncWebServerRequest *request) {
    if(slink.getProgrammerMode()) {
      request->send(400, "text/plain", "Cannot tick the clock while the programmation mode is enabled !"); 
    }
    else if(!slink.getPowerState()) {
      request->send(400, "text/plain", "Cannot tick the clock while the power state is off !"); 
    }
    else {
      ClockInfo clock = slink.getClock();
      if(clock.Source != SLINK_CLOCK || clock.Auto) {
        request->send(400, "text/plain", "The clock source must be SLink and auto-tick disabled");
      }
      else {
        bool fyllCycle = request->getParam("full")->value() == "true";
        slink.tick(fyllCycle);
        request->send(200);
        reportStateUpdated();
      }
    }
  });

  // GET /programmation?state=bool
  server.on("/programmation/state", HTTP_GET, [](AsyncWebServerRequest *request) {
    if(!slink.getPowerState()) {
      request->send(400, "text/plain", "Cannot change the programmation mode while the power state is off !"); 
    } 
    else {
      bool state = request->getParam("state")->value() == "true";
      slink.setProgrammerMode(state);
      programmer.setState(state, true);
      request->send(200);
      reportStateUpdated();
    } 
  });

  // GET /programmation/read?address=int&state=bool
  server.on("/programmation/read", HTTP_GET, [](AsyncWebServerRequest *request) {    
    bool state = request->getParam("state")->value() == "true";
    int address = request->getParam("address")->value().toInt();
    if(!slink.getProgrammerMode()) {
      request->send(400, "text/plain", "The programmation mode is disabled !");
    } 
    else if(address < 0 || address > 0x1FFF) {
      request->send(400, "text/plain", "Invalid address");
    } 
    else {
      programmer.setState(true, false);
      programmer.outputData(address, state);
      request->send(200);
    } 
  });

  // GET /programmation/write?address=int&data=int
  server.on("/programmation/write", HTTP_GET, [](AsyncWebServerRequest *request) {
    int data = request->getParam("data")->value().toInt();
    int address = request->getParam("address")->value().toInt();
    if(!slink.getProgrammerMode()) {
      request->send(400, "text/plain", "The programmation mode is disabled !");
    } 
    else if(address < 0 || address > 0x1FFF) {
      request->send(400, "text/plain", "Invalid address");
    } 
    else if(data < 0 || data > 0xFFFF) {
      request->send(400, "text/plain", "Invalid data");
    } 
    else {
      programmer.setState(true, true);
      programmer.programData(address, data);
      request->send(200);
    } 
  });

  // GET /programmation/erase
  server.on("/programmation/erase", HTTP_GET, [](AsyncWebServerRequest *request) {
    if(slink.getProgrammerMode()) {
      programmer.setState(true, true);
      jobInfo = { true, "Erase all", 0, 0 };
      request->send(200);
    }
    else {
      request->send(400, "text/plain", "The programmation mode is disabled !");
    }    
  });

  // GET /roms
  server.on("/roms", HTTP_GET, [](AsyncWebServerRequest *request) {
    if(request->hasParam("file")) {
      if(!jobInfo.active) {
        String filename = request->getParam("file")->value();
        File file = SPIFFS.open(ROM_DIRECTORY + "/" + filename);
        if(file && file.size() > 0){
          if(request->hasParam("delete")) {
            log_i("Removing %s", file.name());
            SPIFFS.remove(file.path());
            // Notify and return HTTP response
            reportRomsUpdated("Remove");
            request->send(204);
          }
          else if (request->hasParam("newName")) {
            String newName = request->getParam("newName")->value();
            log_i("Renaming %s by %s", file.name(), newName);
            // Create new file
            File destFile = SPIFFS.open(ROM_DIRECTORY + "/" + newName, FILE_WRITE);
            if (!destFile) {
              file.close();
              request->send(400, "text/plain", "Unable to create the new file");
            }
            else {
              // Copy content
              while (file.available()) {
                destFile.write(file.read());
              }
              destFile.close();
              // Remove the old file
              SPIFFS.remove(file.path());
              // Notify and return HTTP response
              reportRomsUpdated("Rename");
              request->send(204);
            }
          }
          else if(request->hasParam("flash")) {
            if(!slink.getProgrammerMode()) {
              request->send(400, "text/plain", "The programmation mode is disabled !"); 
            }
            else if(!slink.getPowerState()) {
              request->send(400, "text/plain", "The power state is off !"); 
            } 
            else {
              log_i("Flashing %s", file.name());
              jobInfo = { true, file.name(), file.size(), 0 };
              file.close();
              request->send(204);
            }
          }
          else {
            request->send(400, "text/plain", "Invalid action");
          }
        }
        else {
          request->send(404, "text/plain", "File not found");
        }
      }
      else {
        request->send(400, "text/plain", "Job in progress");
      }
    }
    else {
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
    }
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
  // Initialize serial monitor
  Serial.begin(115200);  
  delay(1000);

  // Initialize SPIFFS
  if(!SPIFFS.begin(true)){
    log_e("An Error has occurred while mounting SPIFFS");
    return;
  }
  
  // Initialize I²C
  if(!Wire.begin(_SDA, _SCL)) {
    log_e("Unable to initialize I²C bus!");
    return;
  }
  if(!Wire.setClock(I2C_FREQUENCY)) {
    log_e("Unable to set I²C fast mode!");
    return;
  }

  // Initialize S-Link
  if(!slink.begin()) {
    log_e("Unable to initialize SLink!");
    return;
  }

  // Initialize flash programmer
  if(!programmer.begin()) {
    log_e("Unable to initialize Programmer!");
    return;
  }

  // Initialize WiFi
  WiFi.mode(WIFI_STA);
  if(!wm.autoConnect("SLink")) {
    log_e("Failed to connect");
    ESP.restart();
    return;
  }

  // Initialize mDNS
  if (!MDNS.begin("slink")) {
    log_e("Unable to initialize MDNS responder!");
    return;
  }

  // Initialize time
  configTime(3600 * timezone, daysavetime * 3600, "time.nist.gov", "0.pool.ntp.org", "1.pool.ntp.org");

  // Initialize HTTP Server
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
          programmer.eraseChip();
          reportText("Erasing ROM completed!");
        }
        else {
          log_e("Invalid state for erasing flash!");
        }
        jobInfo.active = false;
        reportJob();
    }
    else {
      log_i("Starting flash of %s", jobInfo.romFilename);
      // Notify clients
      reportJob();
      // Open ROM
      File file = SPIFFS.open(ROM_DIRECTORY + "/" + jobInfo.romFilename);
      if(!file){
        log_e("Failed to open file for reading");
        jobInfo.active = false;
        return;
      }
      // Set Programmer mode
      slink.setProgrammerMode(true);
      programmer.setState(true, true);      
      // Erasing flash
      log_i("Erasing flash ...");
      programmer.eraseChip();
      delay(100);
      // Program flash
      uint16_t address = 0;
      while(file.available()) {
        uint16_t msb = file.read();
        if(file.available()) {
          uint16_t lsb = file.read();
          uint16_t data = (uint16_t)msb << 8 | lsb;
          jobInfo.bytesWritten += 2;
          programmer.programData(address++, data);
          reportJob();
        }
      }
      // Disabling programmer mode
      programmer.setState(false);
      slink.setProgrammerMode(false);
      jobInfo.active = false;
      // Notify clients
      reportJob();
      // Job completed
      log_i("File %s written to the flash", jobInfo.romFilename);
    }
  }
  
}