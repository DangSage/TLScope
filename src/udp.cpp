// functionality with UDP portions of the network manager class

#include "network.hpp"
#include <iostream>
#include <string>
#include <memory>

void NetManager::UDPClient(const std::string& ip) {
    std::unique_lock<std::mutex> lock(mtx);
    runningThreads++;
    
    // create a socket
    // send a ping to the ip
    // wait for a response
    // if a response is received, add the ip to the list of online users
    // if no response is received, remove the ip from the list of online users

    while (_running.load()) {
        // do stuff
    }

    lock.lock();
    runningThreads--;
    if (runningThreads == 0) {
        cv.notify_all();
    }
}

void NetManager::UDPServer(const std::string& ip) {
    std::unique_lock<std::mutex> lock(mtx);
    runningThreads++;
    // create a socket
    // listen for pings
    // if a ping is received, send a response

    while (_running.load()) {
        // do stuff
    }

    lock.lock();
    runningThreads--;
    if (runningThreads == 0) {
        cv.notify_all();
    }
}