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
class NetManager {
public:
    // initialize openssl
    // create a new SSL context
    // create socket and open connection
    NetManager();
    ~NetManager();


private:
    SSL_CTX* ctx;
    SSL* ssl;
    int sock;
    std::string ip;
    std::string token;
    std::map<std::string, std::shared_ptr<USER>> onlineUsers;

    void initOpenSSL();
    void cleanupOpenSSL();
    void createSocket(const std::string& ip, int port);
    void createSSLContext();
    void createSSLConnection();
};

#endif // _TLSS_NETWORK_HPP_4204
