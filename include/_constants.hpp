// Constants in TLScope: including strings, maps, and other data types
#ifndef _TLSS_CONSTANTS_HPP_4204
#define _TLSS_CONSTANTS_HPP_4204

#include <string>

// constants for the TLScope project
namespace TLSS_C {

const std::string_view VERSION = "0.0.1";
const std::string_view AUTHOR = "Ethan Dang";
const std::string_view TITLE_ART = R"(
+      _   *      +     .      -       *      ╸     +     x        -   +
  *________  __╸    *   ______  .          *      .      *      ╸         .
  /.       |/. |       /.     \.   *    +     _    -          ,    *    *
  ░░░░░░░░/.░░ |      /░░░░░░  |  _______   ______    ______    ______  
   . ░░ | + ░░ |  *   ░░ \__░░/. /.      | /.     \.+/.     \.*/.     \.
+    ░░ |   ░░ |      ░░      \./░░░░░░░/./░░░░░░  |/░░░░░░  |/░░░░░░  |
     ░░ |*  ░░ |   +   ░░░░░░  |░░ |      ░░ |  ░░ |░░ |  ░░ |░░    ░░ |
  -  ░░ |  .░░ |_____ /. \__░░ |░░ \_____ ░░ \__░░ |░░ |__░░ |░░░░░░░░/.
     ░░ | _ ░░       |░░    ░░/.░░       |░░    ░░/.░░    ░░/.░░       |
     ░░/.   ░░░░░░░░/. ░░░░░░/.  ░░░░░░░/. ░░░░░░/. ░░░░░░░/.  ░░░░░░░/.
* .                                                 ░░ |     ╸     C    
                                                    ░░ |   __┐          
                                                    ░░/.   ╱╲         )";

const std::string SAVE_DIR = "data/";
const std::string SAVE_EXT = ".tlss";
const int PORT = 8080;
}

#endif //._TLSS_CONSTANTS_HPP_4204
