// Utility header file for general manipulation of data
// * included serialization, rng, cryptography, and other general data manipulation
#ifndef _TLSS_UTILS_HPP_4204
#define _TLSS_UTILS_HPP_4204

#include <iostream>
#include <map>
#include <random>
#include <string>
#include <any>

namespace TLSS_U {
    // display a list of items in a map
    std::string displayList(std::map<std::string, std::any> data_dict, std::string prefix="");

    // salt and hash a piece of data using SHA256
    // * returns a pair of strings: the salt and the hashed data
    extern std::pair<std::string, std::string> hash(const std::string& data);

    // verify a piece of data using data and a hash
    extern bool checkHash(const std::string& data, const std::string& salt, const std::string& hashed);

    // gen a key pair for RSA encryption
    extern std::pair<std::string, std::string> genKeyPair();


    // get the ipv4 address of the current machine
    extern std::string getLocalIP(int sockfd);
}

// namespace for user input checking
namespace TLSS_I {
    // prompt+input a email
    // * regex check for email
    extern bool validEmail(std::string& email);

    // prompt+input a password
    // * must be at least 8 characters long
    extern bool validPassword(std::string& password);
}

namespace _rand {
    // Declare a random device
    extern std::random_device rd;
    // Declare a Mersenne Twister pseudo-random generator
    extern std::mt19937 gen;

    // Function template to return a random value within a specified range
    template <typename T>
    T value(const T min, const T max) {
        if (!std::is_arithmetic<T>::value) {
            throw std::invalid_argument("RandomValue() requires a numeric type");
        }
        if (min > max) {
            throw std::invalid_argument("RandomValue() requires min <= max");
        }
        std::uniform_int_distribution<T> dist(min, max);
        return dist(gen);
    }

    // coinflip random function
    extern int value();

    // Function template to pick a random element from a container
    template <typename T>
    T choice(std::initializer_list<T> choices) {
        int randomIndex = value<int>(0, choices.size()-1);
        auto it = choices.begin();
        std::advance(it, randomIndex);

        return *it;
    }

    // Function template to pick a random element from a vector
    template <typename T>
    T choice(const std::vector<T>& choices) {
        int randomIndex = value<int>(0, choices.size()-1);
        return choices[randomIndex];
    }

    // gen a uuid string
    extern std::string uuid();

    // gen a random seed integer
    extern size_t seed();

    // gen a random salt
    std::string genSalt(size_t length);
}  // namespace _rand

#endif // _TLSS_UTILS_HPP_4204