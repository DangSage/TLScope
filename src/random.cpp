// Copyright @ 2024, Ethan Dang
// utility functions for random number generation
#include "_utils.hpp"
#include <random>
#include <bitset>
#include <string>

namespace _rand {
    std::random_device rd;
    std::mt19937 gen(rd());

    int value() {
        std::uniform_int_distribution<int> dist(0, 1);
        return dist(gen);
    }

    size_t seed() {
        std::bitset<16> bits;
        for(int i = 0; i < 16; i++) {
            bits[i] = value();
        }
        size_t seedValue = bits.to_ullong();
        gen.seed(seedValue);
        return seedValue;
    }

    std::string uuid() {
        std::string uuid = "";
        for (int i = 0; i < 32; i++) {
            if (i == 8 || i == 12 || i == 16 || i == 20) {
                uuid += "-";
            }
            uuid += value(0, 15) < 10 ? std::to_string(value(0, 9)) : std::string(1, value('a', 'f'));
        }
        return uuid;
    }
}