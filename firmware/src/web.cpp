// Yaesu ACC spectrum firmware: the Wi-Fi side.
//
// Port 80: the page (web/index.html, built into web_page.h), /status (the
// status text) and /wifi (choose the network). Port 81: a WebSocket that
// sends each frame's first 3102 bytes as one binary message, to up to three
// browsers. Everything here runs on its own task on core 0; the SPI and USB
// work in main.cpp's loop() never waits for it.
//
// The HTTP and WebSocket servers are written here rather than taken from a
// library. Arduino's WebServer serves one connection at a time and waits up
// to 5 s for each to send its request, and phone browsers open spare
// connections in advance that send nothing, so a phone waited 5 s per spare
// connection, or gave up. Here every connection gets a slot, only those that
// have sent a whole request are answered, and every answer closes its
// connection. The WebSocket only ever sends, so its handshake and a 4-byte
// header are all it needs.

#include "web.h"

#include <DNSServer.h>
#include <ESPmDNS.h>
#include <Preferences.h>
#include <WiFi.h>
#include <mbedtls/base64.h>
#include <mbedtls/sha1.h>
#include <mbedtls/version.h>

#include "web_page.h"

static constexpr size_t SEND_BYTES = 3102;       // the FTdx101's data; the FTDX10's ends at 3049
static constexpr int MAX_CLIENTS = 3;
static constexpr int PIN_BOOT = 0;               // the BOOT button
static constexpr uint32_t CONNECT_GIVE_UP_MS = 30000;
static constexpr uint32_t BOOT_HOLD_MS = 3000;   // BOOT held this long opens the setup network
static constexpr uint32_t BUTTON_SETUP_MS = 600000;   // and keeps it open this long
static const char* HOSTNAME = "acc-scope";

static WiFiServer httpServer(80);
static WiFiServer wsServer(81);
static DNSServer dns;
static WiFiClient wsClients[MAX_CLIENTS];
static Preferences wifiPrefs;
static String savedSsid, savedPass, apName;
static volatile bool apOn = false;
// Why the setup network is up: no network saved (until one is), BOOT held
// (for BUTTON_SETUP_MS), or the saved network lost (until it's joined again).
enum class SetupWhy { None, NoNetwork, Button, Fallback };
static SetupWhy setupWhy = SetupWhy::None;
static uint32_t setupSince = 0;
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
    if (WiFi.status() == WL_CONNECTED) {
        // The signal strength, so stalls on the page can be put down to the signal or not.
        int n = snprintf(out, size, "wifi %s rssi %d", WiFi.localIP().toString().c_str(), (int)WiFi.RSSI());
        if (apOn && n > 0 && n < (int)size) snprintf(out + n, size - n, " + setup %s", apName.c_str());
    } else if (apOn) {
        snprintf(out, size, "wifi setup %s", apName.c_str());
    } else {
        snprintf(out, size, "wifi connecting");
    }
}

void webForget() {
    wifiPrefs.remove("ssid");
    wifiPrefs.remove("pass");
}

// --- The setup network and pages -------------------------------------------

static void startSetupNetwork(SetupWhy why) {
    setupWhy = why;
    setupSince = millis();
    if (apOn) return;
    WiFi.mode(savedSsid.isEmpty() ? WIFI_AP : WIFI_AP_STA);
    WiFi.softAP(apName.c_str());
    dns.start(53, "*", WiFi.softAPIP());
    apOn = true;
}

// The setup network is open, with no password, so it only stays up while it's
// needed; the board keeps the saved network and its station side throughout.
static void stopSetupNetwork() {
    if (!apOn) return;
    apOn = false;
    setupWhy = SetupWhy::None;
    dns.stop();
    WiFi.softAPdisconnect(true);
    WiFi.mode(WIFI_STA);
}

// True when the request came in on the setup network rather than the home
// network: only those may see or change the Wi-Fi settings.
static bool fromSetupNetwork(WiFiClient& c) {
    return apOn && c.localIP() == WiFi.softAPIP();
}

// --- HTTP ----------------------------------------------------------------------

static constexpr int SLOTS = 8;                    // connections waiting to send a request
static constexpr uint32_t REQUEST_WAIT_MS = 10000; // one that hasn't sent a whole request by then is closed
static constexpr size_t MAX_REQUEST = 3072;
static constexpr uint32_t SEND_GIVE_UP_MS = 3000;

struct Slot {
    WiFiClient c;
    String in;
    uint32_t since = 0;
    bool ws = false;   // came in on port 81
    bool used = false;
};
static Slot slots[SLOTS];

static void freeSlot(Slot& s) {
    s.c.stop();
    s.in = "";
    s.used = false;
}

static void acceptOn(WiFiServer& server, bool ws) {
    for (;;) {
        WiFiClient c = server.available();
        if (!c) return;
        // A free slot, or else the oldest that has sent nothing yet (a
        // browser's spare connection), or else the oldest.
        Slot* use = nullptr;
        for (auto& s : slots)
            if (!s.used) { use = &s; break; }
        if (!use) {
            for (int pass = 0; pass < 2 && !use; pass++)
                for (auto& s : slots)
                    if ((pass == 1 || s.in.isEmpty()) && (!use || (int32_t)(s.since - use->since) < 0)) use = &s;
            freeSlot(*use);
        }
        use->c = c;
        use->in = "";
        use->since = millis();
        use->ws = ws;
        use->used = true;
    }
}

static bool sendAll(WiFiClient& c, const char* p, size_t n) {
    uint32_t start = millis();
    while (n) {
        size_t w = c.write((const uint8_t*)p, n);
        if (w == 0) {
            if (!c.connected() || millis() - start > SEND_GIVE_UP_MS) return false;
            vTaskDelay(pdMS_TO_TICKS(1));
            continue;
        }
        p += w;
        n -= w;
    }
    return true;
}

static void respond(WiFiClient& c, int code, const char* type, const char* body, size_t len,
                    const char* extraHeaders = "") {
    const char* reason = code == 200 ? "OK" : code == 302 ? "Found" : code == 400 ? "Bad Request"
                       : code == 413 ? "Payload Too Large" : "Not Found";
    char head[384];
    int h = snprintf(head, sizeof head,
                     "HTTP/1.1 %d %s\r\nContent-Type: %s\r\nContent-Length: %u\r\n%sConnection: close\r\n\r\n",
                     code, reason, type, (unsigned)len, extraHeaders);
    if (h > 0 && sendAll(c, head, (size_t)h) && len) sendAll(c, body, len);
}

static void respondText(WiFiClient& c, int code, const char* type, const String& body) {
    respond(c, code, type, body.c_str(), body.length());
}

static void redirect(WiFiClient& c, const char* to) {
    char extra[160];
    snprintf(extra, sizeof extra, "Location: %s\r\n", to);
    respond(c, 302, "text/plain", "", 0, extra);
}

static String urlDecode(const String& s) {
    String o;
    for (size_t i = 0; i < s.length(); i++) {
        char ch = s[i];
        if (ch == '+') {
            o += ' ';
        } else if (ch == '%' && i + 2 < s.length() && isxdigit((unsigned char)s[i + 1]) &&
                   isxdigit((unsigned char)s[i + 2])) {
            char hex[3] = {s[i + 1], s[i + 2], 0};
            o += (char)strtol(hex, nullptr, 16);
            i += 2;
        } else {
            o += ch;
        }
    }
    return o;
}

// One field of an application/x-www-form-urlencoded body.
static String formArg(const String& body, const char* name) {
    String key = String(name) + "=";
    int i = 0;
    while (i <= (int)body.length()) {
        int amp = body.indexOf('&', i);
        if (amp < 0) amp = body.length();
        if (body.startsWith(key, i)) return urlDecode(body.substring(i + key.length(), amp));
        i = amp + 1;
    }
    return "";
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

static void handleWifiForm(WiFiClient& c) {
    if (!fromSetupNetwork(c)) {
        respondText(c, 404, "text/plain", "Not found");
        return;
    }
    String page = WIFI_PAGE_HEAD;
    page += "<form method='post' action='/wifi'><label>Network</label><select name='ssid'>";
    int n = WiFi.scanNetworks();
    // While the station side is still trying to join the saved network, the
    // radio refuses to scan, so the list came back empty. Stop trying, scan,
    // then go back to trying.
    if (n < 0 && !savedSsid.isEmpty() && WiFi.status() != WL_CONNECTED) {
        WiFi.disconnect();
        vTaskDelay(pdMS_TO_TICKS(100));
        n = WiFi.scanNetworks();
        WiFi.begin(savedSsid.c_str(), savedPass.c_str());
    }
    bool any = false;
    for (int i = 0; i < n; i++) {
        String s = WiFi.SSID(i);
        if (s.isEmpty()) continue;
        any = true;
        page += "<option value=\"" + htmlEscape(s) + "\"" + String(s == savedSsid ? " selected" : "") + ">" +
                htmlEscape(s) + "</option>";
    }
    WiFi.scanDelete();
    if (!any) page += n < 0 ? "<option value=''>Couldn't look for networks: type the name below</option>"
                            : "<option value=''>No networks found: type the name below</option>";
    page += "</select><label>Or type its name</label><input name='other' placeholder='Network name' autocapitalize='off'>"
            "<label>Password</label><input name='pass' type='password' autocomplete='off'>"
            "<button type='submit'>Save and restart</button></form>"
            "<p><small>Afterwards the scope is at http://acc-scope.local or at the address the board's status shows.</small></p>"
            "</main></body></html>";
    respondText(c, 200, "text/html", page);
}

static void handleWifiSave(WiFiClient& c, const String& body) {
    if (!fromSetupNetwork(c)) {
        respondText(c, 404, "text/plain", "Not found");
        return;
    }
    String ssid = formArg(body, "other");
    ssid.trim();
    if (ssid.isEmpty()) ssid = formArg(body, "ssid");
    if (ssid.isEmpty()) {
        respondText(c, 400, "text/plain", "No network chosen.");
        return;
    }
    wifiPrefs.putString("ssid", ssid);
    wifiPrefs.putString("pass", formArg(body, "pass"));
    String page = WIFI_PAGE_HEAD;
    page += "<p>Saved <b>" + htmlEscape(ssid) + "</b>. The board is restarting and joining it.</p>"
            "<p>Then open <b>http://acc-scope.local</b>, or the address in the viewer's status.</p></main></body></html>";
    respondText(c, 200, "text/html", page);
    c.flush();
    delay(500);
    ESP.restart();
}

static void handleRoot(WiFiClient& c) {
    if (fromSetupNetwork(c) && WiFi.status() != WL_CONNECTED) {
        redirect(c, "/wifi");
        return;
    }
    // no-cache: after a firmware update the browser fetches the new page, not its old copy.
    respond(c, 200, "text/html; charset=utf-8", WEB_PAGE, sizeof WEB_PAGE - 1, "Cache-Control: no-cache\r\n");
}

static void handleStatus(WiFiClient& c) {
    char text[sizeof statusText];
    if (xSemaphoreTake(statusLock, pdMS_TO_TICKS(50)) == pdTRUE) {
        strlcpy(text, statusText, sizeof text);
        xSemaphoreGive(statusLock);
    } else {
        strlcpy(text, "busy", sizeof text);
    }
    respond(c, 200, "text/plain", text, strlen(text), "Cache-Control: no-store\r\n");
}

static void handleNotFound(WiFiClient& c) {
    if (fromSetupNetwork(c)) redirect(c, "http://192.168.4.1/wifi");   // every address leads to the setup page
    else respondText(c, 404, "text/plain", "Not found");
}

// --- WebSocket ---------------------------------------------------------------

static bool sha1(const uint8_t* in, size_t len, uint8_t out[20]) {
#if MBEDTLS_VERSION_MAJOR >= 3
    return mbedtls_sha1(in, len, out) == 0;
#else
    return mbedtls_sha1_ret(in, len, out) == 0;
#endif
}

// The value of one request header, or "" without it. head is the request's
// header block, lower its lower-case copy.
static String header(const String& head, const String& lower, const char* name) {
    String key = String("\n") + name + ":";
    int at = lower.indexOf(key);
    if (at < 0) return "";
    int from = at + key.length(), to = head.indexOf('\r', from);
    String v = head.substring(from, to < 0 ? head.length() : to);
    v.trim();
    return v;
}

static bool wsHandshake(WiFiClient& c, const String& key) {
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

static void wsTake(WiFiClient& c, const String& key) {
    for (auto& slot : wsClients) {
        if (!slot.connected()) {
            if (wsHandshake(c, key)) slot = c;
            else c.stop();
            return;
        }
    }
    c.stop();   // full
}

// --- Serving the slots ---------------------------------------------------------

static void serveSlot(Slot& s) {
    char buf[256];
    while (s.c.available() && s.in.length() < MAX_REQUEST) {
        size_t room = MAX_REQUEST - s.in.length();
        int n = s.c.read((uint8_t*)buf, room < sizeof buf ? room : sizeof buf);
        if (n <= 0) break;
        for (int k = 0; k < n; k++) s.in += buf[k];
    }
    int end = s.in.indexOf("\r\n\r\n");
    if (end < 0) {
        if (s.in.length() >= MAX_REQUEST) {
            respondText(s.c, 413, "text/plain", "Request too large");
            freeSlot(s);
        } else if (!s.c.connected() || millis() - s.since > REQUEST_WAIT_MS) {
            freeSlot(s);
        }
        return;
    }
    String head = s.in.substring(0, end), lower = head;
    lower.toLowerCase();
    size_t want = (size_t)header(head, lower, "content-length").toInt();
    String body = s.in.substring(end + 4);
    if (body.length() < want) {   // the rest of a POST is still coming
        if (s.in.length() >= MAX_REQUEST) {
            respondText(s.c, 413, "text/plain", "Request too large");
            freeSlot(s);
        } else if (!s.c.connected() || millis() - s.since > REQUEST_WAIT_MS) {
            freeSlot(s);
        }
        return;
    }
    if (s.ws) {
        wsTake(s.c, header(head, lower, "sec-websocket-key"));   // the WebSocket keeps the connection
        s.c = WiFiClient();
        s.in = "";
        s.used = false;
        return;
    }
    int sp1 = head.indexOf(' '), sp2 = head.indexOf(' ', sp1 + 1);
    String method = head.substring(0, sp1 < 0 ? 0 : sp1);
    String path = sp1 < 0 ? String("") : head.substring(sp1 + 1, sp2 < 0 ? head.length() : sp2);
    int q = path.indexOf('?');
    if (q >= 0) path = path.substring(0, q);
    bool get = method == "GET" || method == "HEAD";
    if (path == "/" && get) handleRoot(s.c);
    else if (path == "/status" && get) handleStatus(s.c);
    else if (path == "/wifi" && get) handleWifiForm(s.c);
    else if (path == "/wifi" && method == "POST") handleWifiSave(s.c, body.substring(0, want));
    else handleNotFound(s.c);
    freeSlot(s);
}

static void serveHttp() {
    acceptOn(httpServer, false);
    acceptOn(wsServer, true);
    for (auto& s : slots)
        if (s.used) serveSlot(s);
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

// BOOT is a strapping pin: held at power-on it starts the ROM's download mode
// and this firmware never runs. So it's read here, while running: held for
// BOOT_HOLD_MS, it opens the setup network.
static bool bootHeld() {
    static uint32_t downSince = 0;
    static bool fired = false;
    if (digitalRead(PIN_BOOT) != LOW) {
        downSince = 0;
        fired = false;
        return false;
    }
    if (!downSince) downSince = millis() | 1;
    if (fired || millis() - downSince < BOOT_HOLD_MS) return false;
    fired = true;
    return true;
}

static void webTask(void*) {
    uint32_t lastSeq = 0;
    uint32_t lastConnected = millis();
    bool mdnsOn = false;
    for (;;) {
        if (apOn) dns.processNextRequest();
        serveHttp();
        bool connected = WiFi.status() == WL_CONNECTED;
        if (connected) lastConnected = millis();
        if (connected && !mdnsOn) {
            mdnsOn = MDNS.begin(HOSTNAME);
            if (mdnsOn) MDNS.addService("http", "tcp", 80);
        }
        if (bootHeld()) startSetupNetwork(savedSsid.isEmpty() ? SetupWhy::NoNetwork : SetupWhy::Button);
        // Can't join the saved network: make the setup network as well, so it
        // can be changed, and keep trying the saved one.
        if (!connected && !apOn && !savedSsid.isEmpty() && millis() - lastConnected > CONNECT_GIVE_UP_MS)
            startSetupNetwork(SetupWhy::Fallback);
        // Close it again once it isn't needed: when the saved network is back,
        // or when BOOT's time is up and the board is on the saved network.
        if (apOn && connected && WiFi.softAPgetStationNum() == 0 &&
            (setupWhy == SetupWhy::Fallback ||
             (setupWhy == SetupWhy::Button && millis() - setupSince > BUTTON_SETUP_MS)))
            stopSetupNetwork();
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

    WiFi.persistent(false);
    WiFi.setHostname(HOSTNAME);
    WiFi.setSleep(false);   // keeps the stream smooth
    if (savedSsid.isEmpty()) {
        startSetupNetwork(SetupWhy::NoNetwork);
    } else {
        WiFi.mode(WIFI_STA);
        WiFi.setAutoReconnect(true);
        WiFi.begin(savedSsid.c_str(), savedPass.c_str());
    }

    httpServer.begin();
    wsServer.begin();

    xTaskCreatePinnedToCore(webTask, "web", 8192, nullptr, 1, nullptr, 0);
}
