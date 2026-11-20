// Yaesu ACC spectrum firmware: the Wi-Fi side.
//
// Port 80: the page (web/index.html, built into web_page.h), /status (the
// status text) and /wifi (choose the network). Port 81: a WebSocket that
// sends each frame's first 3102 bytes as one binary message, to up to three
// browsers. Everything here runs on its own task on core 0; the SPI and USB
// work in main.cpp's loop() never waits for it.
//
// The WebSocket server is written here rather than taken from a library: it
// only ever sends, so the handshake and a 4-byte header are all it needs.

#include "web.h"

#include <DNSServer.h>
#include <ESPmDNS.h>
#include <Preferences.h>
#include <WebServer.h>
#include <WiFi.h>
#include <mbedtls/base64.h>
#include <mbedtls/sha1.h>
#include <mbedtls/version.h>

#include "web_page.h"

static constexpr size_t SEND_BYTES = 3102;       // the FTdx101's data; the FTDX10's ends at 3049
static constexpr int MAX_CLIENTS = 3;
static constexpr int PIN_BOOT = 0;               // the BOOT button
static constexpr uint32_t CONNECT_GIVE_UP_MS = 30000;
static const char* HOSTNAME = "acc-scope";

static WebServer http(80);
static WiFiServer wsServer(81);
static DNSServer dns;
static WiFiClient wsClients[MAX_CLIENTS];
static Preferences wifiPrefs;
static String savedSsid, savedPass, apName;
static volatile bool apOn = false;
static volatile int clientCount = 0;

static SemaphoreHandle_t frameLock;
static uint8_t shared[SEND_BYTES];
static size_t sharedLen = 0;
static volatile uint32_t sharedSeq = 0;
static uint8_t sendBuf[4 + SEND_BYTES];

static SemaphoreHandle_t statusLock;
static char statusText[320] = "ACC pipe: starting";

void webPublishFrame(const uint8_t* data, size_t len) {
    if (!frameLock || clientCount == 0) return;
    if (xSemaphoreTake(frameLock, 0) != pdTRUE) return;
    size_t n = len < SEND_BYTES ? len : SEND_BYTES;
    memcpy(shared, data, n);
    sharedLen = n;
    sharedSeq++;
    xSemaphoreGive(frameLock);
}

void webSetStatusText(const char* text) {
    if (!statusLock || xSemaphoreTake(statusLock, 0) != pdTRUE) return;
    strlcpy(statusText, text, sizeof statusText);
    xSemaphoreGive(statusLock);
}

void webStatus(char* out, size_t size) {
    if (WiFi.status() == WL_CONNECTED)
        snprintf(out, size, "wifi %s", WiFi.localIP().toString().c_str());
    else if (apOn)
        snprintf(out, size, "wifi setup %s", apName.c_str());
    else
        snprintf(out, size, "wifi connecting");
}

void webForget() {
    wifiPrefs.remove("ssid");
    wifiPrefs.remove("pass");
}

// --- The setup network and pages -------------------------------------------

static void startSetupNetwork() {
    if (apOn) return;
    WiFi.mode(savedSsid.isEmpty() ? WIFI_AP : WIFI_AP_STA);
    WiFi.softAP(apName.c_str());
    dns.start(53, "*", WiFi.softAPIP());
    apOn = true;
}

static String htmlEscape(const String& s) {
    String o;
    for (char c : s) {
        if (c == '<') o += "&lt;";
        else if (c == '>') o += "&gt;";
        else if (c == '&') o += "&amp;";
        else if (c == '"') o += "&quot;";
        else o += c;
    }
    return o;
}

static const char WIFI_PAGE_HEAD[] =
    "<!doctype html><html><head><meta charset='utf-8'><meta name='viewport' content='width=device-width,initial-scale=1'>"
    "<title>ACC Scope Wi-Fi</title><style>body{font:16px system-ui,sans-serif;background:#111;color:#eee;margin:0;padding:16px}"
    "main{max-width:420px;margin:auto}input,select,button{font:inherit;width:100%;box-sizing:border-box;padding:10px;margin:6px 0 14px;"
    "border-radius:6px;border:1px solid #555;background:#222;color:#eee}button{background:#2d6cdf;border:0;cursor:pointer}"
    "small{color:#aaa}</style></head><body><main><h2>ACC Scope Wi-Fi</h2>";

static void handleWifiForm() {
    String page = WIFI_PAGE_HEAD;
    page += "<form method='post' action='/wifi'><label>Network</label><select name='ssid'>";
    int n = WiFi.scanNetworks();
    bool any = false;
    for (int i = 0; i < n; i++) {
        String s = WiFi.SSID(i);
        if (s.isEmpty()) continue;
        any = true;
        page += "<option" + String(s == savedSsid ? " selected" : "") + ">" + htmlEscape(s) + "</option>";
    }
    WiFi.scanDelete();
    if (!any) page += "<option value=''>No networks found</option>";
    page += "</select><label>Or type its name</label><input name='other' placeholder='Network name' autocapitalize='off'>"
            "<label>Password</label><input name='pass' type='password' autocomplete='off'>"
            "<button type='submit'>Save and restart</button></form>"
            "<p><small>Afterwards the scope is at http://acc-scope.local or at the address the board's status shows.</small></p>"
            "</main></body></html>";
    http.send(200, "text/html", page);
}

static void handleWifiSave() {
    String ssid = http.arg("other");
    ssid.trim();
    if (ssid.isEmpty()) ssid = http.arg("ssid");
    if (ssid.isEmpty()) {
        http.send(400, "text/plain", "No network chosen.");
        return;
    }
    wifiPrefs.putString("ssid", ssid);
    wifiPrefs.putString("pass", http.arg("pass"));
    String page = WIFI_PAGE_HEAD;
    page += "<p>Saved <b>" + htmlEscape(ssid) + "</b>. The board is restarting and joining it.</p>"
            "<p>Then open <b>http://acc-scope.local</b>, or the address in the viewer's status.</p></main></body></html>";
    http.send(200, "text/html", page);
    delay(500);
    ESP.restart();
}

static void handleRoot() {
    if (apOn && WiFi.status() != WL_CONNECTED) {
        http.sendHeader("Location", "/wifi");
        http.send(302, "text/plain", "");
        return;
    }
    http.send(200, "text/html", WEB_PAGE);
}

static void handleStatus() {
    char text[sizeof statusText];
    if (xSemaphoreTake(statusLock, pdMS_TO_TICKS(50)) == pdTRUE) {
        strlcpy(text, statusText, sizeof text);
        xSemaphoreGive(statusLock);
    } else {
        strlcpy(text, "busy", sizeof text);
    }
    http.sendHeader("Cache-Control", "no-store");
    http.send(200, "text/plain", text);
}

static void handleNotFound() {
    if (apOn) {   // the setup network: send every address to the setup page
        http.sendHeader("Location", "http://192.168.4.1/wifi");
        http.send(302, "text/plain", "");
    } else {
        http.send(404, "text/plain", "Not found");
    }
}

// --- WebSocket ---------------------------------------------------------------

static bool sha1(const uint8_t* in, size_t len, uint8_t out[20]) {
#if MBEDTLS_VERSION_MAJOR >= 3
    return mbedtls_sha1(in, len, out) == 0;
#else
    return mbedtls_sha1_ret(in, len, out) == 0;
#endif
}

static bool wsHandshake(WiFiClient& c) {
    c.setTimeout(2);   // seconds
    String key;
    for (int lines = 0; lines < 40; lines++) {
        String line = c.readStringUntil('\n');
        line.trim();
        if (line.isEmpty()) break;
        String lower = line;
        lower.toLowerCase();
        if (lower.startsWith("sec-websocket-key:")) {
            key = line.substring(18);
            key.trim();
        }
    }
    if (key.isEmpty()) return false;
    String src = key + "258EAFA5-E914-47DA-95CA-C5AB0DC85B11";
    uint8_t digest[20];
    if (!sha1((const uint8_t*)src.c_str(), src.length(), digest)) return false;
    unsigned char accept[40];
    size_t olen = 0;
    if (mbedtls_base64_encode(accept, sizeof accept - 1, &olen, digest, sizeof digest) != 0) return false;
    accept[olen] = 0;
    c.printf("HTTP/1.1 101 Switching Protocols\r\nUpgrade: websocket\r\nConnection: Upgrade\r\n"
             "Sec-WebSocket-Accept: %s\r\n\r\n", (const char*)accept);
    c.setNoDelay(true);
    return true;
}

static void wsAccept() {
    WiFiClient c = wsServer.available();
    if (!c) return;
    for (auto& slot : wsClients) {
        if (!slot.connected()) {
            if (wsHandshake(c)) slot = c;
            else c.stop();
            return;
        }
    }
    c.stop();   // full
}

static void wsSendLatest(uint32_t& lastSeq) {
    if (sharedSeq == lastSeq) return;
    size_t n;
    if (xSemaphoreTake(frameLock, pdMS_TO_TICKS(5)) != pdTRUE) return;
    n = sharedLen;
    memcpy(sendBuf + 4, shared, n);
    lastSeq = sharedSeq;
    xSemaphoreGive(frameLock);
    sendBuf[0] = 0x82;   // final fragment, binary
    sendBuf[1] = 126;    // 16-bit length follows
    sendBuf[2] = (uint8_t)(n >> 8);
    sendBuf[3] = (uint8_t)n;
    for (auto& c : wsClients) {
        if (!c.connected()) continue;
        if (c.write(sendBuf, 4 + n) != 4 + n) c.stop();   // too slow or gone: drop it
    }
}

static void wsTidy() {
    int count = 0;
    for (auto& c : wsClients) {
        if (!c.connected()) {
            if (c) c.stop();
            continue;
        }
        while (c.available()) c.read();   // pings and close frames: the browser reconnects if it wants to
        count++;
    }
    clientCount = count;
}

// --- The task ----------------------------------------------------------------

static void webTask(void*) {
    uint32_t lastSeq = 0;
    uint32_t started = millis();
    bool mdnsOn = false;
    for (;;) {
        if (apOn) dns.processNextRequest();
        http.handleClient();
        bool connected = WiFi.status() == WL_CONNECTED;
        if (connected && !mdnsOn) {
            mdnsOn = MDNS.begin(HOSTNAME);
            if (mdnsOn) MDNS.addService("http", "tcp", 80);
        }
        // Can't join the saved network: make the setup network as well, so it
        // can be changed, and keep trying the saved one.
        if (!connected && !apOn && millis() - started > CONNECT_GIVE_UP_MS) startSetupNetwork();
        wsAccept();
        wsTidy();
        if (clientCount) wsSendLatest(lastSeq);
        vTaskDelay(pdMS_TO_TICKS(2));
    }
}

void webSetup() {
    frameLock = xSemaphoreCreateMutex();
    statusLock = xSemaphoreCreateMutex();
    wifiPrefs.begin("wifi", false);
    savedSsid = wifiPrefs.getString("ssid", "");
    savedPass = wifiPrefs.getString("pass", "");

    char name[24];
    uint64_t mac = ESP.getEfuseMac();
    snprintf(name, sizeof name, "ACC-Scope-%02X%02X", (uint8_t)(mac >> 32), (uint8_t)(mac >> 40));
    apName = name;

    pinMode(PIN_BOOT, INPUT_PULLUP);
    bool setupWanted = savedSsid.isEmpty() || digitalRead(PIN_BOOT) == LOW;

    WiFi.persistent(false);
    WiFi.setHostname(HOSTNAME);
    WiFi.setSleep(false);   // keeps the stream smooth
    if (setupWanted) {
        startSetupNetwork();
    } else {
        WiFi.mode(WIFI_STA);
        WiFi.setAutoReconnect(true);
        WiFi.begin(savedSsid.c_str(), savedPass.c_str());
    }

    http.on("/", HTTP_GET, handleRoot);
    http.on("/status", HTTP_GET, handleStatus);
    http.on("/wifi", HTTP_GET, handleWifiForm);
    http.on("/wifi", HTTP_POST, handleWifiSave);
    http.onNotFound(handleNotFound);
    http.begin();
    wsServer.begin();

    xTaskCreatePinnedToCore(webTask, "web", 8192, nullptr, 1, nullptr, 0);
}
