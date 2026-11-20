# PlatformIO extra script: before each build, put web/index.html into
# src/web_page.h as a C string, so the page is built into the firmware.
Import("env")
import os

project = env.subst("$PROJECT_DIR")
html = open(os.path.join(project, "web", "index.html"), encoding="utf-8").read()
if ')ACCHTML"' in html:
    raise SystemExit("web/index.html contains the string's end marker")
header = ("// Made by embed_web.py from web/index.html at build time. Don't edit.\n"
          "#pragma once\n"
          'static const char WEB_PAGE[] = R"ACCHTML(' + html + ')ACCHTML";\n')
path = os.path.join(project, "src", "web_page.h")
if not os.path.exists(path) or open(path, encoding="utf-8").read() != header:
    open(path, "w", encoding="utf-8").write(header)
