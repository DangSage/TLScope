// Constants in TLScope: including strings, maps, and other data types
#ifndef _TLSS_CONSTANTS_HPP_4204
#define _TLSS_CONSTANTS_HPP_4204

#include <string>
#include <array>
#include <regex>

// constants for the TLScope project
namespace TLSS_C {

const std::string_view VERSION = "0.0.1";
const std::string_view AUTHOR = "Ethan Dang";
const std::string_view TITLE_ART =
"\n+      _   *      +     .      -       *      ╸     +     x        -   +"
"\n  *________  __╸    *   ______  .          *      .      *      ╸         ."
"\n  /.       |/. |       /.     \\.   *    +     _    -          ,    *    *"
"\n  ░░░░░░░░/.░░ |      /░░░░░░  |  _______   ______    ______    ______  "
"\n   . ░░ | + ░░ |  *   ░░ \\__░░/. /.      | /.     \\.+/.     \\.*/.     \\."
"\n+    ░░ |   ░░ |      ░░      \\./░░░░░░░/./░░░░░░  |/░░░░░░  |/░░░░░░  |    +"
"\n     ░░ |*  ░░ |   +   ░░░░░░  |░░ |      ░░ |  ░░ |░░ |  ░░ |░░    ░░ |   ,"
"\n  -  ░░ |  .░░ |_____ /. \\__░░ |░░ \\_____ ░░ \\__░░ |░░ |__░░ |░░░░░░░░/."
"\n     ░░ | _ ░░       |░░    ░░/.░░       |░░    ░░/.░░    ░░/.░░       |"
"\n     ░░/.   ░░░░░░░░/. ░░░░░░/.  ░░░░░░░/. ░░░░░░/. ░░░░░░░/.  ░░░░░░░/."
"\n* .                                                 ░░ |     ╸     *  _ "
"\n                                                    ░░ |    \033[33m__┐\033[0m "
"\n                                                    ░░/.    \033[33m╱\\\033[0m";

const std::string SAVE_DIR = "data/";
const std::string SAVE_EXT = ".tlss";
const int PORT = 3000;

const std::regex email_regex(R"([a-zA-Z0-9._%+-]+@[a-zA-Z0-9.-]+\.[a-zA-Z]{2,})");
}

#endif //._TLSS_CONSTANTS_HPP_4204
