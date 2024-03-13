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