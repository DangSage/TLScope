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

NetManager::NetManager(std::string uuid) : _ctxT(nullptr), _uPort(TLSS_C::PORT+10),
    _uSocket(-1) {
    auto t = TLSS_U::hash(uuid);
    _token = t.first + ':' + t.second;
    std::cout << "Token: " << _token << std::endl;
    // create the SSL context
    _ctxT = SSL_CTX_new(TLS_client_method());
    std::cout << "Creating context..." << std::endl;
    if (_ctxT == nullptr) {
        ERR_print_errors_fp(stderr);
        exit(1);
    }
    std::cout << "SSL context created" << std::endl;

    if (!SSL_CTX_load_verify_locations(_ctxT, "ca-cert.pem", nullptr)) {
        ERR_print_errors_fp(stderr);
        exit(1);
    }

    // start the UDP client thread
    _udpClient = std::thread(&NetManager::udpHandler, this);
}

NetManager::~NetManager() {
    _running = false;
    _udpClient.join();
    SSL_CTX_free(_ctxT);
}
