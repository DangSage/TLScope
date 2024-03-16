// Network header: network class and methods
// * will contain all network related classes and methods
#ifndef _TLSS_NETWORK_HPP_4204
#define _TLSS_NETWORK_HPP_4204

#include <iostream>
#include <string>
#include <memory>
#include <openssl/ssl.h>
#include <mutex>
#include <vector>
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


    // UDP client thread to ping devices listening on the designated port
    void UDPClient(const std::string& ip);
    
    // UDP server thread to listen for pings on the designated port
    void UDPServer(const std::string& ip);

    // TCP client thread to send messages
    void TCPClient(const std::string& ip, int port);

    // TCP server thread to listen for messages 
    void TCPServer(const std::string& ip, int port);


private:
    SSL_CTX* ctx;
    SSL* ssl;
    int sock;
    std::string ip;
    std::string token;
    std::map<std::string, std::shared_ptr<USER>> onlineUsers;
    std::mutex onlineUsersMutex;

    void initOpenSSL();
    void cleanupOpenSSL();
    void createSocket(const std::string& ip, int port);
    void createSSLContext();
    void createSSLConnection();
};

#endif // _TLSS_NETWORK_HPP_4204
