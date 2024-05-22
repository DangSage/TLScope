// Display formatting functions for the user interface.

#include <string>
#include <sstream>
#include <iostream>
#include <regex>
#include <map>
#include <any>

#include "_utils.hpp"
#include "user.hpp"
#include "_constants.hpp"

std::string TLSS_U::displayList(const std::map<std::string, std::any>& data_dict, std::string prefix) {
    std::stringstream ss;
    if (data_dict.empty()) {
        ss << prefix << " └─No items.\n";
        return ss.str();
    }

    auto it = data_dict.begin();
    while (it != data_dict.end()) {
        std::string key = it->first;
        std::string new_prefix = prefix + ((std::next(it) == data_dict.end()) ? "  " : " │");

        ss << prefix << ((std::next(it) == data_dict.end()) ? " └─" : " ├─") << key;
        // display ss.str() in color

        if (it->second.type() == typeid(std::map<std::string, std::any>)) {
            const std::map<std::string, std::any>& nested_map = std::any_cast<const std::map<std::string, std::any>&>(it->second);
            ss << ":" << std::endl << displayList(nested_map, new_prefix + "  ");
        } else {
            ss << ": " << std::any_cast<std::string>(it->second) << std::endl;
        }
        ++it;
    }

    return ss.str();
}

bool TLSS_I::validEmail(std::string& email) {
    while (true) {
        std::cout << "Enter email address  -> ";
        std::cin >> std::ws;
        std::getline(std::cin, email);
        // return true;
        // regex check for email
        if (std::regex_match(email, TLSS_C::email_regex)) {
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