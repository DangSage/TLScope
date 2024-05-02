// Header file for the TLScope class.
// * Main wrapper for the entire program
#ifndef _TLSS_MAIN_HPP_4204
#define _TLSS_MAIN_HPP_4204

#include "network.hpp"
#include <string>
#include <memory>
#include <map>

// forward declarations
class USER;

// TLScope class
class TLScope {
public:
    TLScope();
    TLScope(const std::string &name);

    // entry point
    void start();

    // run the TLScope application
    void run();

    // save user data to a file
    bool saveUserData(std::shared_ptr<USER> user);

    // register a new user
    bool registerUser();

    // login a user
    bool loginUser();

    // get user data in a formatted tree
    void getUserData();
private:
    bool newUser = false;
    std::shared_ptr<USER> user;
    std::map<std::string, std::shared_ptr<USER>> registered_users;
    std::map<std::string, std::shared_ptr<USER>> online_users;

    // network manager
    std::unique_ptr<NetManager> netManager;
};

#endif // _TLSS_MAIN_HPP_4204
