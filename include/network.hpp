// Network header: network class and methods
// * will contain all network related classes and methods
#ifndef _TLSS_NETWORK_HPP_4204
#define _TLSS_NETWORK_HPP_4204

#include <iostream>
#include <string>
#include <memory>
#include <openssl/ssl.h>
#include <vector>
#include <map>

// forward declarations
class USER;

// network class
// * will contain all network related classes and methods
// * needs to be able to send and receive data
class NETWORK {
public:
    NETWORK() = default;

private:
    // my ip address
    std::string ip;
    // session token
    std::string token;
    // list of online users on the network
    std::map<std::string, USER> onlineUsers;
    // list of trusted users on the network
    std::map<std::string, USER> trustedUsers;
    // list of contact requests received by the client
    std::vector<std::string> contactRequestsReceived;
    // list of pending contact requests sent by the client
    std::vector<std::string> pendingContactRequests;
    // list of ports to ignore when broadcasting
    std::vector<int> portsToIgnore;
};

#endif // _TLSS_NETWORK_HPP_4204
