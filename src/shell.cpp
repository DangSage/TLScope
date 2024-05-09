// Shell for the TLScope class

#include "TLScope.hpp"
#include "user.hpp"
#include "_utils.hpp"
#include "_constants.hpp"
#include <iostream>
#include <string>
#include <functional>
#include <atomic>
#include <csignal>
#include <map>


void TLScope::shell() {

    std::map<std::string, std::string> commandDescriptions = {
        {"\033[31m^C", "Quit\033[0m"},
        {" h", "Help"},
        {" m", "My Data"},
        {" u", "User Data"}
    };

    std::map<std::string, std::function<void()>> commandHandlers = {
        {"h", [&]() { 
            for (auto& [command, description] : commandDescriptions) {
                std::cout << command << " -> " << description << std::endl;
            }
        }},
        {"m", [&]() { 
            if (!getUserData()) {
                std::cout << "Error getting user data." << std::endl;
            }
        }},
        {"u", [&]() {
            if (netManager->_users.empty()) {
                std::cout << "No users found." << std::endl;
                return;
            }
            std::cout << "\033[32mUsers on the network:\033[0m" << std::endl;
            std::map<std::string, std::any> usersMap;
            for (auto& [token, user] : netManager->_users) {
                usersMap[user->IPP] = std::map<std::string, std::any> {
                    {"name", user->name},
                    {"email", user->email},
                    {"token", token.substr(0,16)}
                };
            }
            std::cout << TLSS_U::displayList(usersMap) << std::endl;
        }}
    };

    while (netManager->_running) {
        std::cout << "\033[33m$ TLScope> \033[0m";
        std::string command;
        std::getline(std::cin, command);

        auto handler = commandHandlers.find(command);
        if (handler != commandHandlers.end()) {
            handler->second();
        } else {
            std::cout << "Invalid command!" << std::endl;
            commandHandlers["h"]();
        }
    }
}