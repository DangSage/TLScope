// Display formatting functions for the user interface.
#include "_utils.hpp"
#include "user.hpp"
#include <string>
#include <sstream>
#include <iostream>
#include <regex>
#include <map>
#include <any>

std::string TLSS_U::displayList(std::map<std::string, std::any> 
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
                ss << displayList(
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

bool TLSS_I::validEmail(std::string& email) {
    while (true) {
        std::cout << "Enter email address  -> ";
        std::cin >> std::ws;
        std::getline(std::cin, email);
        // return true;
        // regex check for email
        if (std::regex_match(email, 
        std::regex(R"(\b[A-Za-z0-9._%+-]+@[A-Za-z0-9.-]+\.[A-Z|a-z]{2,}\b)"))) {
            return true;
        } else if (email == "q") {
            return false;
        }
        std::cerr << "Error: Invalid email address!" << std::endl;
    }
}

bool TLSS_I::validPassword(std::string& password) {
    while (true) {
        std::cout << "Enter user password  -> ";
        std::cin >> std::ws;
        std::getline(std::cin, password);
        // return true;
        // must be at least 10 characters long
        if (password.length() >= 10) {
            return true;
        } else if (password == "q") {
            return false;
        }
        std::cerr << "Error: Password too short! (<10 characters)" << std::endl;
    }
}