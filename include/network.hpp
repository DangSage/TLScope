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
    // initialize openssl
    // create a new SSL context
    // create socket and open connection
    // run the threads for the client and server connections
    NetManager();
    ~NetManager();

    // start threads for the client and server connections
    void threads();

    // kill the threads for the client and server connections
    void kill();

    // UDP client thread to ping devices listening on the designated port
    void UDPClient(const std::string& ip);
    
    // UDP server thread to listen for pings on the designated port
    void UDPServer(const std::string& ip);

    // // TCP client thread to send messages
    // void TCPClient(const std::string& ip, int port);

    // // TCP server thread to listen for messages 
    // void TCPServer(const std::string& ip, int port);

    std::atomic<bool> _running;

private:
    SSL_CTX* _ctx;
    SSL* _ssl;
    std::string _ip;
    std::string _token;

    std::condition_variable cv; // condition variable for thread safety
    std::mutex mtx;             // mutex for thread safety
    int runningThreads = 0;     // number of running threads

    std::map<std::string, std::shared_ptr<USER>> onlineUsers;
    std::mutex onlineUsersMutex;

    std::thread _udpClient;
    std::thread _udpServer;

    void initOpenSSL();
    void cleanupOpenSSL();
    int createSocket(const std::string& ip, int port);
};

#endif // _TLSS_NETWORK_HPP_4204
