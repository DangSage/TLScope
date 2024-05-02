// Main wrapper for the entire program
#include "TLScope.hpp"
#include "user.hpp"
#include "_utils.hpp"
#include "_constants.hpp"
#include <iostream>
#include <string>
#include <map>


TLScope::TLScope(): user(nullptr), netManager(nullptr) {
    registered_users = buildRegisteredUsers();
    newUser = registered_users.empty();
}

TLScope::TLScope(const std::string &name) {
    user = std::make_shared<USER>();
    user->name = name;
}

void TLScope::start() {
    std::cout << TLSS_C::TITLE_ART << "\033[1A\r     " << "Version: "
        << TLSS_C::VERSION << " | Author: " << TLSS_C::AUTHOR << " [G]" << std::endl
        << "     GNU General Public License v3.0 - 2021" << std::endl << std::endl;

    if (newUser) { std::cout << "No users registered. Please register a new user.\n" << std::endl; }

    std::cout << " R. Register" << std::endl;
    if (!newUser) { std::cout << " L. Login" << std::endl; }
    std::cout << " Q. Quit (q to quit)" << std::endl;
    std::cout << "─────────────────────────────────────────────" << std::endl;

    char input;
    while (true) {
        std::cout << "$ TLScope> ";
        std::cin >> input;
        input = std::toupper(input);
        if (input == 'Q') {
            return;
        } else if (input == 'L') {
            if (!loginUser()) { return; }
            break;
        } else if (input == 'R') {
            if (!registerUser()) {
                user.reset(); // reset user to nullptr
                return;
            }
            break;
        } else if (input == 'G') {
            std::string url = "https://github.com/DangSage/TLScope";
            std::cout << "$> visit @" << url << std::endl;
        } else {
            std::cout << "Invalid input!" << std::endl;
        }
    }
}

void TLScope::run() {
    start();
    // if user is logged in, start the main program
    if (user != nullptr) {
        netManager = std::make_unique<NetManager>(user->uuid);
        std::cout << "Welcome, " << user->name << "!" << std::endl;
        shell();

        netManager->_udpClient.join();
        netManager.reset();
    }
    std::cout << "Closing TLScope..." << std::endl;
}

bool TLScope::getUserData() {
    try {
        std::cout << "My user data:" << std::endl;
        TLSS_U::displayList(std::map<std::string, std::any> {
            {"name", user->name},
            {"email", user->email},
            {"uuid", user->uuid}
        });
    } catch (const std::exception &e) {
        std::cerr << "Error getting user data: " << e.what() << std::endl;
        return false;
    }
    return true;
}