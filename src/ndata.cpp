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
    // Initialize OpenSSL
    SSL_library_init();
    SSL_load_error_strings();
    OpenSSL_add_all_algorithms();

    // Create a new SSL context
    _ctx = SSL_CTX_new(DTLS_method());
    if (_ctx == NULL) {
        // Handle error
        exit(1);
    }

    // Set options on the context (for example, load certificates)
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
    return sock;
}

void NetManager::cleanupOpenSSL() {
    EVP_cleanup();
}

NetManager::NetManager(): _running(true){
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

    _udpClient = std::thread(&NetManager::udpPing, this);
    _udpServer = std::thread(&NetManager::udpReceive, this);

    // wait for the threads to boot up
    std::this_thread::sleep_for(std::chrono::seconds(2));
}

NetManager::~NetManager() {
    if (_udpServer.joinable()) {
        _udpServer.join();
        std::cout << " ├─UDP server closed" << std::endl;
    }
    if (_udpClient.joinable()) {
        _udpClient.join();
        std::cout << " ├─UDP client closed" << std::endl;
    }
    cleanupOpenSSL();
    std::cout << " └─NetManager closed." << std::endl;
}
