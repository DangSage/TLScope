// functionality with UDP portions of the network manager class

#include "network.hpp"
#include <iostream>
#include <string>
#include <memory>

void NetManager::UDPClient(const std::string& ip) {
    // create a socket
    // send a ping to the ip
    // wait for a response
    // if a response is received, add the ip to the list of online users
    // if no response is received, remove the ip from the list of online users
    std::cout << "UDP client thread started" << std::endl;

    while (_running.load()) {
        // do stuff
    }

    std::cout << " ├─UDP client closed\n";
}

void NetManager::UDPServer(const std::string& ip) {
    // create a socket
    // listen for pings
    // if a ping is received, send a response
    std::cout << "UDP server thread started" << std::endl;

    while (_running.load()) {
        // do stuff
    }

    std::cout << " ├─UDP server closed\n";
}