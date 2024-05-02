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

NetManager::NetManager() : _ctxU(nullptr), _uPort(TLSS_C::PORT+10) {
    // create the SSL context
    _ctxU = SSL_CTX_new(DTLS_client_method());
    std::cout << "Creating context..." << std::endl;
    if (_ctxU == nullptr) {
        ERR_print_errors_fp(stderr);
        exit(1);
    }
    std::cout << "SSL context created" << std::endl;

    if (!SSL_CTX_load_verify_locations(_ctxU, "ca-cert.pem", nullptr)) {
        ERR_print_errors_fp(stderr);
        exit(1);
    }

    // start the UDP client thread
    _udpClient = std::thread(&NetManager::udpHandler, this);
}

NetManager::~NetManager() {
    _running = false;
    _udpClient.join();
    SSL_CTX_free(_ctxU);
}

int NetManager::createSocket(const std::string& ip, int port) {
    int sockfd = socket(AF_INET, SOCK_DGRAM, 0);
    if (sockfd < 0) {
        perror("Cannot create socket");
        exit(1);
    }

    struct sockaddr_in addr;
    memset(&addr, 0, sizeof(addr));
    addr.sin_family = AF_INET;
    addr.sin_port = htons(port);
    if (inet_pton(AF_INET, ip.c_str(), &(addr.sin_addr)) <= 0) {
        perror("Invalid IP address");
        exit(1);
    }

    if (bind(sockfd, (struct sockaddr*)&addr, sizeof(addr)) < 0) {
        perror("Cannot bind socket");
        exit(1);
    }

    return sockfd;
}