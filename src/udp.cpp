// functionality with UDP portions of the network manager class

#include <iostream>
#include <string>
#include <memory>
#include <chrono>
#include <thread>
#include <future>

#include <sys/socket.h>
#include <netinet/in.h>
#include <arpa/inet.h>
#include <unistd.h>

#include "network.hpp"
#include "_constants.hpp"
#include "_utils.hpp"

void NetManager::udpHandler() {
    int sockfd;

    // Creating socket file descriptor
    if ((sockfd = socket(AF_INET, SOCK_DGRAM, 0)) < 0) {
        perror("socket creation failed");
        exit(EXIT_FAILURE);
    }
    struct sockaddr_in servaddr, cliaddr;

    int broadcastEnable = 1;
    int ret = setsockopt(sockfd, SOL_SOCKET, SO_BROADCAST, &broadcastEnable, sizeof(broadcastEnable));
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

    if (setsockopt(sockfd, SOL_SOCKET, SO_RCVTIMEO, &timeout, sizeof(timeout)) < 0) {
        perror("Error: could not set recv timeout");
        exit(EXIT_FAILURE);
    }

    while (true) {
        servaddr.sin_port = htons(_uPort);
        if (bind(sockfd, (const struct sockaddr *)&servaddr, sizeof(servaddr)) < 0) {
            servaddr.sin_port = htons(_uPort++);
        } else {
            break;
        }
    }

    std::cout << "listening at " << _ip << ':' << _uPort << std::endl;
    std::string message = "ping";

    int len, n;
    len = sizeof(cliaddr);
    char buffer[1024];
    std::string receivedMessage;
    receivedMessage.reserve(1024);
    int port = TLSS_C::PORT+10;
    int endPort = TLSS_C::PORT+20;

    while (_running) {
        // Create a future for the send operation
        auto send_future = std::async(std::launch::async, [&]() {
            servaddr.sin_addr.s_addr = INADDR_BROADCAST;
            port > endPort ? port = TLSS_C::PORT+10 : port++;
            servaddr.sin_port = htons(port);
            int bytesSent = sendto(sockfd, (const char *)message.c_str(), message.size(),
                MSG_CONFIRM, (const struct sockaddr *) &servaddr, 
                sizeof(servaddr));
            if (bytesSent == -1) {
                perror("sendto error");
            }
        });

        // Create a future for the receive operation
        auto recv_future = std::async(std::launch::async, [&]() {
            servaddr.sin_addr.s_addr = INADDR_ANY;
            n = recvfrom(sockfd, buffer, sizeof(buffer) - 1, MSG_WAITALL,
            (struct sockaddr *) &cliaddr, (socklen_t*)&len);
            if (n >= 0) {
                buffer[n] = '\0';
                receivedMessage.assign(buffer);
            } else {
                if (errno == EWOULDBLOCK) {
                    // timeout, no data received
                    return;
                } else {
                    perror("recvfrom error");
                    _running = false;
                }
            }
        });

        // Wait for both futures to complete
        send_future.get();
        recv_future.get();

        if (receivedMessage != "ping" && !receivedMessage.empty()) {
            std::cout << inet_ntoa(cliaddr.sin_addr) << ":" << ntohs(cliaddr.sin_port)
                << " -> " << receivedMessage << std::endl;
            continue;
        }

        if (ntohs(cliaddr.sin_port) == _uPort) {
            // ignore messages from the same port
            continue;
        }
        // Always send a "pong" response back to the client
        std::string response = "pong";
        sendto(sockfd, (const char *)response.c_str(), response.size(),
            MSG_CONFIRM, (const struct sockaddr *) &cliaddr, len);
    }
    close(sockfd);
}

