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
    NetManager();
    ~NetManager();

    // Thread for the UDP client
    std::thread _udpClient;

private:
    // running flag for the threads (atomic)
    std::atomic<bool> _running = true;

    // start the UDP client
    void udpHandler();

    int createSocket(const std::string& ip, int port);

    // context for the SSL connection (UDP)
    SSL_CTX *_ctxU;
    // UDP port
    int _uPort;

    std::string _ip;
    std::string _token;
};

#endif // _TLSS_NETWORK_HPP_4204
