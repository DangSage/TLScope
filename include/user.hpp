// User data header file
// * will be used for all users (client and connections)
#ifndef _TLSS_USER_HPP_4204
#define _TLSS_USER_HPP_4204

#include <boost/serialization/vector.hpp>
#include <string>
#include <map>
#include <memory>

// user class for the user data
// * Will be used to save and load user data
// * Will store user data including the main user and users that are online
struct USER {
    std::string name;
    std::string email;
    std::string hashedPassword;
    int color;

    // std::vector<std::string> character;
    std::string uuid;

    // encoding and decoding functions
    template <class Archive>
    void serialize(Archive& ar, const unsigned int version) {
        ar & name;
        ar & email;
        ar & hashedPassword;
        ar & color;
        ar & uuid;
    }
};

// build list of registered users on file, attached by uuid
std::map<std::string, std::shared_ptr<USER>> buildRegisteredUsers();

// load user data from a file
std::shared_ptr<USER> loadUserData(const std::string& uuid);

// load user data from a file

#endif // _TLSS_USER_HPP_4204
