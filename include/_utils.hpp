// Utility header file for general manipulation of data
// * will contain general utility functions for data manipulation
#ifndef _TLSS_UTILS_HPP_4204
#define _TLSS_UTILS_HPP_4204

#include <iostream>
#include <map>
#include <sstream>
#include <random>
#include <string>
#include <any>

namespace TLSS_U {
    // display a list of items in a map
    std::string display_list(std::map<std::string, std::any> data_dict, std::string prefix="");
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

    // generate a uuid string
    extern std::string uuid();

    // generate a random seed integer
    extern size_t seed();
}  // namespace _rand

#endif // _TLSS_UTILS_HPP_4204