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

#include <sys/socket.h>
#include <netinet/in.h>

// forward declarations
class USER;

// network class
// * will contain all network related classes and methods
// * needs to be able to send and receive data
// ** UDP connections for device discovery (ping w/ broadcast)
// ** TCP connections for sending and receiving messages (TLS)
class NetManager {
public:
    explicit NetManager(std::string uuid);
    ~NetManager();

    // Thread for the UDP client
    std::thread _udpClient;
    std::map<std::string, std::shared_ptr<USER>> _users;

    // running flag for the threads (atomic)
    std::atomic<bool> _running = true;
private:
    struct sockaddr_in servaddr, cliaddr;
    SSL_CTX *_ctxT;

    int _uPort;
    int _uSocket;

    std::string _ip;
    std::string _token;

    std::map<std::string, std::shared_ptr<USER>> known_users;

    // start the UDP client
    void udpHandler();
};

#endif // _TLSS_NETWORK_HPP_4204
