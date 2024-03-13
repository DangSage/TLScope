#include "_utils.hpp"
#include "user.hpp"
#include <string>
#include <map>
#include <any>

std::string TLSS_U::display_list(std::map<std::string, std::any> 
    data_dict, std::string prefix) {
    std::stringstream ss;
    if (data_dict.empty()) {
        ss << prefix << " └─No items.\n";
    } else {
        size_t i = 0;
        for (auto& kv : data_dict) {
            std::string key = kv.first;
            std::string new_prefix = prefix + 
                ((i == data_dict.size() - 1) ? "  " : " │");
            if (kv.second.type() == typeid(std::map<std::string, std::any>)) {
                ss << prefix << ((i == data_dict.size() - 1) ? " └─" : " ├─") 
                    << key << ":\n";
                ss << display_list(
                    std::any_cast<std::map<std::string, std::any>>(kv.second),
                    new_prefix + "  ");
            } else {
                ss << prefix << ((i == data_dict.size() - 1) ? " └─" : " ├─")
                    << key << ": " << std::any_cast<std::string>(kv.second) << "\n";
            }
            i++;
        }
    }
    std::cout << ss.str();
    return ss.str();
}