// All data manipulation and methods related to the Network class

#include <iostream>

#include <string>
#include <sys/socket.h>
#include <netinet/in.h>
#include <arpa/inet.h>
#include <ifaddrs.h>
#include <netdb.h>
#include <unistd.h>

#include <openssl/ssl.h>
#include <openssl/err.h>
#include <openssl/bio.h>

#include "network.hpp"
#include "_constants.hpp"
#include "_utils.hpp"
#include "user.hpp"

NetManager::NetManager(std::string uuid) : _ctxT(nullptr), _uPort(TLSS_C::PORT),
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

    mreq.imr_multiaddr.s_addr = inet_addr("224.0.0.1"); // replace with your multicast address
    mreq.imr_interface.s_addr = htonl(INADDR_ANY);
    if (setsockopt(_uSocket, IPPROTO_IP, IP_ADD_MEMBERSHIP, (void *) &mreq, sizeof(mreq)) < 0) {
        perror("setsockopt - IP_ADD_MEMBERSHIP");
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

    _ip = TLSS_U::getLocalIP();

    // start the UDP client thread
    _udpClient = std::thread(&NetManager::udpHandler, this);

    std::cout << "Hosting on: " << _ip << ":" << _uPort << std::endl;
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
    user.name = "\"User "+std::to_string(_users.size()+1)+"\"";
    user.IPP = std::string(inet_ntoa(cliaddr.sin_addr)) + ":" + std::to_string(ntohs(cliaddr.sin_port));
    return std::make_shared<USER>(user);
}

std::string TLSS_U::getLocalIP() {
    struct ifaddrs * ifAddrStruct = NULL;
    struct ifaddrs * ifa = NULL;
    void * tmpAddrPtr = NULL;

    getifaddrs(&ifAddrStruct);

    for (ifa = ifAddrStruct; ifa != NULL; ifa = ifa->ifa_next) {
        if (!ifa->ifa_addr) {
            continue;
        }
        // check it is IP4
        if (ifa->ifa_addr->sa_family == AF_INET) { 
            tmpAddrPtr = &((struct sockaddr_in *)ifa->ifa_addr)->sin_addr;
            char addressBuffer[INET_ADDRSTRLEN];
            inet_ntop(AF_INET, tmpAddrPtr, addressBuffer, INET_ADDRSTRLEN);
            if (strcmp(ifa->ifa_name, "lo") != 0) { // exclude loopback
                if (ifAddrStruct != NULL) freeifaddrs(ifAddrStruct);
                return std::string(addressBuffer);
            }
        }
    }
    if (ifAddrStruct != NULL) freeifaddrs(ifAddrStruct);
    return "";
}
