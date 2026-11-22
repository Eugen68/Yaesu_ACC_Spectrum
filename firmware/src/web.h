// Yaesu ACC spectrum firmware: the Wi-Fi side. A web page with the scope, and
// the frames to it over a WebSocket. The USB pipe in main.cpp is unchanged.
#pragma once

#include <Arduino.h>

// Starts Wi-Fi and the web server, on their own task on core 0. With no
// network saved, with BOOT held for 3 s, or when the saved network can't be
// joined for 30 s, the board makes its own network, ACC-Scope-xxxx, with a
// setup page at http://192.168.4.1/wifi, and closes it again once it isn't
// needed.
void webSetup();

// Hands a frame to the web task. Never waits: if the web task is busy with
// the last one, this one is skipped. Only the first 3102 bytes are sent.
void webPublishFrame(const uint8_t* data, size_t len);

// The Wi-Fi state for the status text, such as "wifi 192.168.1.20",
// "wifi connecting" or "wifi setup ACC-Scope-1A2B".
void webStatus(char* out, size_t size);

// The last status text, served at /status.
void webSetStatusText(const char* text);

// Forgets the saved network, so the next start makes the setup network.
void webForget();
