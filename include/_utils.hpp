#ifndef _TLSS_UTILS_HPP_4204
#define _TLSS_UTILS_HPP_4204

#include <iostream>
#include <map>
#include <sstream>
#include <any>

namespace TLSS_U {
    // display a list of items in a map
    std::string display_list(std::map<std::string, std::any> data_dict, std::string prefix="");
}


#endif // _TLSS_UTILS_HPP_4204