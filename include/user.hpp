#ifndef _TS_USER_HEADER_4204
#define _TS_USER_HEADER_4204

#include <boost/serialization/vector.hpp>
#include <fstream>
#include <string>
#include <vector>

// user class for the user data
// * Will be used to save and load user data
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

#endif