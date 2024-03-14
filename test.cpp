// Copyright Ethan Dang 2024
#undef timeout
#include <iostream>
#include <string>
#include <filesystem>

#define BOOST_TEST_DYN_LINK
#define BOOST_TEST_MODULE TLScopeTest
#include <boost/test/unit_test.hpp>

#include "TLScope.hpp"
#include "user.hpp"
#include "_utils.hpp"

BOOST_AUTO_TEST_CASE(TLScopeTest) {
    TLScope ts("Ethan");
    BOOST_CHECK_NO_THROW(ts.getUserData());
}

BOOST_AUTO_TEST_CASE(SaltTest) {
    std::string salt = _rand::genSalt(16);
    // genSalt is 16 bytes, so the size of the salt should be 32
    BOOST_CHECK_EQUAL(salt.size(), 32);
}

BOOST_AUTO_TEST_CASE(hashTest) {
    std::string data = "thisisastring";
    std::pair<std::string, std::string> hashed = TLSS_U::hash(data);
    BOOST_CHECK_NE(data, hashed.second);
    BOOST_CHECK(TLSS_U::checkHash(data, hashed.first, hashed.second));
}