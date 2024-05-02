// functionality with UDP portions of the network manager class

#include <iostream>
#include <string>
#include <memory>
#include <chrono>
#include <thread>

#include <sys/socket.h>
#include <netinet/in.h>
#include <arpa/inet.h>
#include <unistd.h>

#include "network.hpp"
#include "_constants.hpp"

void NetManager::udpHandler() {
    int sockfd;

    // Creating socket file descriptor
    if ((sockfd = socket(AF_INET, SOCK_DGRAM, 0)) < 0) {
        perror("socket creation failed");
        exit(EXIT_FAILURE);
    }

    // Start udpReceive in a new thread
    std::thread udpReceiveThread(&NetManager::udpReceive, this, sockfd);

    // Start udpPing in a new thread
    std::thread udpPingThread(&NetManager::udpPing, this, sockfd);

    udpReceiveThread.join();
    udpPingThread.join();

    close(sockfd);

    std::cout << "UDP Handler finished" << std::endl;
}

void NetManager::udpPing(int sockfd) {
    struct sockaddr_in servaddr;

    int broadcastEnable = 1;
    int ret = setsockopt(sockfd, SOL_SOCKET, SO_BROADCAST, &broadcastEnable, sizeof(broadcastEnable));
    if (ret) {
        perror("Error: could not enable broadcast option on socket");
        exit(EXIT_FAILURE);
    }

    memset(&servaddr, 0, sizeof(servaddr));

    // Filling server information
    servaddr.sin_family = AF_INET;
    servaddr.sin_addr.s_addr = INADDR_BROADCAST;

    std::string message = "ping:"+std::to_string(_uPort);

    int endPort = TLSS_C::PORT+20;

    while (_running) {
        for (int port = TLSS_C::PORT+10; port <= endPort; ++port) {
            servaddr.sin_port = htons(port);
            int bytesSent = sendto(sockfd, (const char *)message.c_str(), message.size(),
                MSG_CONFIRM, (const struct sockaddr *) &servaddr, 
                sizeof(servaddr));
            if (bytesSent == -1) {
                perror("sendto error");
            }

            std::this_thread::sleep_for(std::chrono::milliseconds(500));
        }
    }

    close(sockfd);
}

void NetManager::udpReceive(int sockfd) {
    struct sockaddr_in servaddr, cliaddr;

    memset(&servaddr, 0, sizeof(servaddr));
    memset(&cliaddr, 0, sizeof(cliaddr));

    // Filling server information
    servaddr.sin_family = AF_INET;
    servaddr.sin_addr.s_addr = INADDR_ANY;

    // Bind the socket with the server address
    while (true) {
        servaddr.sin_port = htons(_uPort);
        if (bind(sockfd, (const struct sockaddr *)&servaddr, sizeof(servaddr)) < 0) {
            servaddr.sin_port = htons(_uPort++);
        } else {
            break;
        }
    }

    std::cout << "listening on port " << _uPort << std::endl;

    int len, n;
    len = sizeof(cliaddr);

    char buffer[1024];

    while (_running) {
        n = recvfrom(sockfd, (char *)buffer, 1024, MSG_WAITALL,
            (struct sockaddr *) &cliaddr, (socklen_t*)&len);
        buffer[n] = '\0';
        std::string message(buffer);

        // if the message is a response, print the client address
        if (message == "pong") {
            std::cout << inet_ntoa(cliaddr.sin_addr) << ":"
            << ntohs(cliaddr.sin_port) << std::endl;
            continue;
        }
        
        // send a response back to the client address on the port in the message
        int port = std::stoi(message.substr(message.find(":")+1));
        if (port == _uPort) { continue; }
        std::string response = "pong";

        // set the port in cliaddr to the target port
        cliaddr.sin_port = htons(port);

        sendto(sockfd, (const char *)response.c_str(), response.size(),
            MSG_CONFIRM, (const struct sockaddr *) &cliaddr, len);
        std::this_thread::sleep_for(std::chrono::milliseconds(500));
    }

    close(sockfd);
}