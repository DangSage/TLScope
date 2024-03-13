// Header file for the TLScope class.
// * Main wrapper for the entire program
#ifndef _TLSS_MAIN_HPP_4204
#define _TLSS_MAIN_HPP_4204

#include <iostream>
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

    // run the TLScope
    void run();

    // get user data in a formatted tree
    void getUserData();
private:
    std::shared_ptr<USER> user;
    std::map<std::string, std::shared_ptr<USER>> registered_users;
    std::map<std::string, std::shared_ptr<USER>> online_users;
};

#endif // _TLSS_MAIN_HPP_4204
