// All data manipulation and methods related to the Network class

#include <iostream>

#include <string>
#include <sys/socket.h>
#include <netinet/in.h>
#include <arpa/inet.h>
#include <netdb.h>
#include <unistd.h>

#include <openssl/ssl.h>
#include <openssl/err.h>
#include <openssl/bio.h>

#include "network.hpp"
#include "_constants.hpp"
#include "_utils.hpp"
#include "user.hpp"

std::string TLSS_U::getLocalIP(int sockfd) {
    struct sockaddr_in localAddress;
    socklen_t addressLength = sizeof(localAddress);

    if (getsockname(sockfd, (struct sockaddr*)&localAddress, &addressLength) == -1) {
        perror("getsockname");
        return "";
    }

    char buffer[INET_ADDRSTRLEN];
    const char* p = inet_ntop(AF_INET, &localAddress.sin_addr, buffer, INET_ADDRSTRLEN);

    return p ? std::string(p) : "";
}

NetManager::NetManager(std::string uuid) : _ctxT(nullptr), _uPort(TLSS_C::PORT+10),
    _uSocket(-1) {
    // ** TLS Setup ===========================================================

    auto t = TLSS_U::hash(uuid);
    _token = t.first + ':' + t.second;
    std::cout << "Token: " << _token << std::endl;
    // create the SSL context
    _ctxT = SSL_CTX_new(TLS_client_method());
    if (_ctxT == nullptr) {
        ERR_print_errors_fp(stderr);
        exit(1);
    }

    if (!SSL_CTX_load_verify_locations(_ctxT, "ca-cert.pem", nullptr)) {
        ERR_print_errors_fp(stderr);
        exit(1);
    }

    // ** UDP Setup ===========================================================

    if ((_uSocket = socket(AF_INET, SOCK_DGRAM, 0)) < 0) {
        perror("socket creation failed");
        exit(EXIT_FAILURE);
    }

    int broadcastEnable = 1;
    int ret = setsockopt(_uSocket, SOL_SOCKET, SO_BROADCAST, &broadcastEnable, sizeof(broadcastEnable));
    if (ret) {
        perror("Error: could not enable broadcast option on socket");
        exit(EXIT_FAILURE);
    }

    memset(&servaddr, 0, sizeof(servaddr));
    memset(&cliaddr, 0, sizeof(cliaddr));

    // Filling server information
    servaddr.sin_family = AF_INET; // IPv4

    struct timeval timeout;
    timeout.tv_sec = 0;
    timeout.tv_usec = 500000;

    if (setsockopt(_uSocket, SOL_SOCKET, SO_RCVTIMEO, &timeout, sizeof(timeout)) < 0) {
        perror("Error: could not set recv timeout");
        exit(EXIT_FAILURE);
    }

    while (true) {
        servaddr.sin_port = htons(_uPort);
        if (bind(_uSocket, (const struct sockaddr *)&servaddr, sizeof(servaddr)) < 0) {
            servaddr.sin_port = htons(_uPort++);
        } else {
            break;
        }
    }

    // start the UDP client thread
    _udpClient = std::thread(&NetManager::udpHandler, this);
}

NetManager::~NetManager() {
    _running = false;
    _udpClient.join();
    SSL_CTX_free(_ctxT);
}

void NetManager::removeInactiveUsers() {
    for(auto it = _users.begin(); it != _users.end(); ) {
        if (it->second->lastHeartbeat < std::chrono::system_clock::now() - std::chrono::seconds(2)) {
            it = _users.erase(it);
        } else {
            ++it;
        }
    }
}

std::shared_ptr<USER> NetManager::createUser(const std::string& token, const sockaddr_in& cliaddr) {
    USER user;
    user.name = "\"User "+std::to_string(_users.size())+"\"";
    user.IPP = std::string(inet_ntoa(cliaddr.sin_addr)) + ":" + std::to_string(ntohs(cliaddr.sin_port));
    return std::make_shared<USER>(user);
}
