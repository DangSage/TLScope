#include "TLScope.hpp"
#include "user.hpp"

TLScope::TLScope() {
    user = std::make_shared<USER>();
}

TLScope::TLScope(const std::string &name) {
    user = std::make_shared<USER>();
    user->name = name;
}

void TLScope::run() {
    std::cout << "Running TLScope..." << std::endl;
    std::cout << "User: " << user->name << std::endl;
}