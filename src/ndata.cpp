// All data manipulation and methods related to the Network class

#include <iostream>

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

NetManager::NetManager() : _ctxU(nullptr), _uPort(TLSS_C::PORT+10) {
    char hostname[128];
    gethostname(hostname, sizeof(hostname));
    struct hostent* host = gethostbyname(hostname);
    struct in_addr** addr_list = (struct in_addr**)host->h_addr_list;
    _ip = inet_ntoa(*addr_list[0]);
    std::cout << "IP: " << _ip << std::endl;

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

    udpHandler();
}

NetManager::~NetManager() {
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