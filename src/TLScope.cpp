// Main wrapper for the entire program
#include "TLScope.hpp"
#include "user.hpp"
#include "_utils.hpp"
#include "_constants.hpp"
#include <iostream>
#include <string>
#include <map>

TLScope::TLScope(): user(std::make_shared<USER>()) {
    registered_users = buildRegisteredUsers();
    newUser = registered_users.empty();
}

TLScope::TLScope(const std::string &name) {
    user = std::make_shared<USER>();
    user->name = name;
}

void TLScope::run() {
    std::cout << "Running TLScope..." << std::endl;
    std::cout << TLSS_C::TITLE_ART << "\033[1A\r     " << "Version: "
        << TLSS_C::VERSION << "  Author: " << TLSS_C::AUTHOR << std::endl
        << "     GNU General Public License v3.0 - 2021";

    std::cout << std::endl << std::endl;
    if (newUser) { std::cout << "No users registered. Please register a new user.\n" << std::endl; }

    std::cout << " r. Register" << std::endl;
    if (!newUser) { std::cout << " l. Login" << std::endl; }
    std::cout << " q. Quit" << std::endl;
    std::cout << "─────────────────────────────────────────────" << std::endl;

    char input;
    while (true) {
        std::cout << ">";
        std::cin >> input;
        if (input == 'q') {
            break;
        } else if (input == 'l') {
            break;
        } else if (input == 'r') {
            registerUser();
            break;
        } else {
            std::cout << "Invalid input!" << std::endl;
        }
    }
    std::cout << "Quitting..." << std::endl;
}

void TLScope::getUserData() {
    std::cout << "USER DATA:" << std::endl;
    TLSS_U::displayList(std::map<std::string, std::any> {
        {"name", user->name},
        {"email", user->email},
        {"uuid", user->uuid}
    });
}