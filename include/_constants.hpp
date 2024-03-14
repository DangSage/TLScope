// Constants in TLScope: including strings, maps, and other data types
#ifndef _TLSS_CONSTANTS_HPP_4204
#define _TLSS_CONSTANTS_HPP_4204

#include <string>

namespace TLSS_C {

const std::string_view VERSION = "0.0.1";
const std::string_view AUTHOR = "Ethan Dang";
const std::string_view TITLE_ART = R"(
   ________  __         ______                                          
  /.       |/. |       /.     \.                                        
  $$$$$$$$/.$$ |      /$$$$$$  |  _______   ______    ______    ______  
     $$ |   $$ |      $$ \__$$/. /.      | /.     \. /.     \. /.     \.
     $$ |   $$ |      $$      \./$$$$$$$/./$$$$$$  |/$$$$$$  |/$$$$$$  |
     $$ |   $$ |       $$$$$$  |$$ |      $$ |  $$ |$$ |  $$ |$$    $$ |
     $$ |   $$ |_____ /. \__$$ |$$ \_____ $$ \__$$ |$$ |__$$ |$$$$$$$$/.
     $$ |   $$       |$$    $$/.$$       |$$    $$/.$$    $$/.$$       |
     $$/.   $$$$$$$$/. $$$$$$/.  $$$$$$$/. $$$$$$/. $$$$$$$/.  $$$$$$$/.
                                                    $$ |                
                                                    $$ |                
                                                    $$/.                )";

const std::string SAVE_DIR = "data/";
const std::string SAVE_EXT = ".tlss";

}

#endif //._TLSS_CONSTANTS_HPP_4204
