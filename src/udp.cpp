// functionality with UDP portions of the network manager class

#include "network.hpp"
#include <iostream>
#include <string>
#include <memory>

// UDP client thread to ping devices listening on the designated port
void NetManager::UDPClient(const std::string& ip) {
    // create a socket
    // send a ping to the ip
    // wait for a response
    // if a response is received, add the ip to the list of online users
    // if no response is received, remove the ip from the list of online users
    std::cout << "UDP client thread started" << std::endl;
    
}