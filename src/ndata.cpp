// All data manipulation and methods related to the Network class

#include "network.hpp"
#include "_constants.hpp"
#include <unistd.h>
#include <netdb.h>
#include <netinet/in.h>
#include <arpa/inet.h>
#include <sys/socket.h>
#include <thread>

void NetManager::initOpenSSL() {
    SSL_library_init();
    SSL_load_error_strings();
    ERR_load_BIO_strings();
    OpenSSL_add_all_algorithms();
}

int NetManager::createSocket(const std::string& ip, int port) {
    struct sockaddr_in addr;
    addr.sin_family = AF_INET;
    addr.sin_port = htons(port);
    addr.sin_addr.s_addr = inet_addr(ip.c_str());

    auto sock = socket(AF_INET, SOCK_STREAM, 0);
    if (sock == -1) {
        perror("socket");
        exit(1);
    }

    if (connect(sock, (struct sockaddr*)&addr, sizeof(addr)) == -1) {
        perror("connect");
        exit(1);
    }
}

void NetManager::cleanupOpenSSL() {
    EVP_cleanup();
}

NetManager::NetManager() {
    // *Need to be defined
    initOpenSSL();

    // Get the hostname of the device
    char hostname[128];
    gethostname(hostname, sizeof(hostname));

    // Get the hostent structure for the hostname
    struct hostent* host = gethostbyname(hostname);
    if (host == NULL) {
        // Handle error - the host is unknown
        perror("gethostbyname");
        exit(1);
    }

    struct in_addr **addr_list = (struct in_addr **) host->h_addr_list;
    _ip = inet_ntoa(*addr_list[0]);
    
    // start the server and client threads
    std::thread serverThread(&NetManager::UDPServer, this, _ip, TLSS_C::PORT);
    std::thread clientThread(&NetManager::UDPClient, this, _ip, TLSS_C::PORT);
}

NetManager::~NetManager() {
    cleanupOpenSSL();
    std::cout << "NetManager closed." << std::endl;
}