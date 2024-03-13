#include "TLScope.hpp"
#include "user.hpp"
#include "_utils.hpp"
#include "_constants.hpp"
#include <iostream>
#include <string>
#include <map>

TLScope::TLScope(): user(std::make_shared<USER>()) {
    user->name = "username";
    user->email = "user@email.com";
    user->hashedPassword = "password";
    user->color = 0x000000;
    user->uuid = "00000000-0000-0000-0000-000000000000";
}

TLScope::TLScope(const std::string &name) {
    user = std::make_shared<USER>();
    user->name = name;
}

void TLScope::run() {
    std::cout << "Running TLScope..." << std::endl;
    std::cout << TLSS_C::TITLE_ART << std::endl;
}

void TLScope::getUserData() {
    std::cout << "USER DATA:" << std::endl;
    TLSS_U::display_list(std::map<std::string, std::any> {
        {"name", user->name},
        {"email", user->email},
        {"hashedPassword", user->hashedPassword},
        {"color", std::to_string(user->color)},
        {"uuid", user->uuid}
    });
}