// Copyright @ 2024, Ethan Dang
#include "_constants.hpp"
#include "_utils.hpp"
#include "TLScope.hpp"
#include "user.hpp"
#include <boost/archive/binary_oarchive.hpp>
#include <boost/archive/binary_iarchive.hpp>
#include <boost/archive/archive_exception.hpp>
#include <filesystem>
#include <fstream>
#include <iostream>
#include <limits>
#include <chrono>
#include <thread>

bool TLScope::saveUserData(std::shared_ptr<USER> user) {
    std::cout << "Saving..." << std::endl;
    if(user->uuid.empty()) {
        user->uuid = _rand::uuid();
    }
    std::string filedir = TLSS_C::SAVE_DIR 
        + user->uuid + TLSS_C::SAVE_EXT;

    std::ofstream ofs(filedir, std::ios::binary);
    if(!ofs) { // check if file opened successfully
        std::cerr << "Error opening file for saving: " << filedir << std::endl;
        return false;
    }
    try {
        boost::archive::binary_oarchive oa(ofs);
        oa << *user;
    } catch (boost::archive::archive_exception& e) {
        std::cerr << "Error saving character: " << e.what() << std::endl;
        return false;
    }
    std::this_thread::sleep_for(std::chrono::seconds(1));
    std::cout << "Player " << user->name << " saved to " << filedir << std::endl;
    return true;
}

std::shared_ptr<USER> loadUserData(const std::string& uuid) {
    std::string filedir = TLSS_C::SAVE_DIR + uuid + TLSS_C::SAVE_EXT;
    std::shared_ptr<USER> user = std::make_shared<USER>(); // Initialize the shared_ptr
    if(!std::filesystem::exists(filedir)) {
        throw std::runtime_error("Error: File does not exist: " + filedir);
    }
    std::ifstream ifs(filedir, std::ios::binary);
    if(!ifs) {
        throw std::runtime_error("Error opening file for loading: " + filedir);
    }
    try {
        boost::archive::binary_iarchive ia(ifs);
        ia >> *user;
    } catch (boost::archive::archive_exception& e) {
        throw std::runtime_error("Error loading character: " + std::string(e.what()));
    }
    
    return user;
}

bool TLScope::registerUser() {
    while (true) {
        std::cout << "Enter client name    -> ";
        std::cin >> std::ws;
        std::getline(std::cin, user->name);
        std::cout << "Enter email address  -> ";
        std::cin >> std::ws;
        std::getline(std::cin, user->email);
        std::cout << "Enter user password  -> ";
        std::cin >> std::ws;
        std::getline(std::cin, user->hashedPassword);
        std::cout << "Enter color (in hex) -> ";
        std::cin >> std::ws >> std::hex >> user->color;
        std::cout << std::endl;

        if (user->name.empty()) {
            std::cerr << "Error: Name cannot be empty!" << std::endl;
            return false;
        }
        auto it = std::find_if(registered_users.begin(), registered_users.end(), [&](std::pair<std::string, std::shared_ptr<USER>> pair) {
            return pair.second->email == user->email;
        });

        if (it != registered_users.end()) {
            std::cerr << "Error: User already exists!" << std::endl;
            return false;
        }

        break;
    }

    return saveUserData(user);
}

std::map<std::string, std::shared_ptr<USER>> buildRegisteredUsers() {
    std::map<std::string, std::shared_ptr<USER>> list = {};
    if (!std::filesystem::exists(TLSS_C::SAVE_DIR)) {
        std::filesystem::create_directory(TLSS_C::SAVE_DIR);
    }
    for(const auto& entry : std::filesystem::directory_iterator(TLSS_C::SAVE_DIR)) {
        // only load .sav files
        if(entry.path().extension() != TLSS_C::SAVE_EXT) {
            continue;
        }
        std::string filedir = entry.path().string();
        std::ifstream ifs(filedir, std::ios::binary);
        if(!ifs) {
            throw std::runtime_error("Error opening file for loading: " + filedir);
        }
        try {
            boost::archive::binary_iarchive ia(ifs);
            std::shared_ptr<USER> temp = std::make_shared<USER>();
            ia >> *temp;  // dereference the shared_ptr to get the USER object
            list[temp->uuid] = temp;
        } catch (boost::archive::archive_exception& e) {
            throw std::runtime_error("Error loading character: " + std::string(e.what()));
        }
    }
    return list;
}
