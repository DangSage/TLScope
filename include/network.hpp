// Network header: network class and methods
// * will contain all network related classes and methods
#ifndef _TLSS_NETWORK_HPP_4204
#define _TLSS_NETWORK_HPP_4204

#include <iostream>
#include <string>
#include <memory>
#include <openssl/ssl.h>
#include <condition_variable>
#include <mutex>
#include <vector>
#include <atomic>
#include <thread>
#include <map>

// forward declarations
class USER;

// network class
// * will contain all network related classes and methods
// * needs to be able to send and receive data
// ** UDP connections for device discovery (ping w/ DTLS)
// ** TCP connections for sending and receiving messages (TLS)
class NetManager {
public:
    NetManager() = default;
    ~NetManager() = default;


    // UDP client thread to ping devices listening on the designated port
    void udpPing();
    
    // UDP server thread to listen for pings on the designated port
    void udpReceive();

private:
    std::string _ip;
    std::string _token;
};

#endif // _TLSS_NETWORK_HPP_4204
