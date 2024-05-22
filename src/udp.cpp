// functionality with UDP portions of the network manager class

#include <iostream>
#include <string>
#include <memory>
#include <algorithm>
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
#include "user.hpp"

void NetManager::udpHandler() {
    int len, n;
    len = sizeof(cliaddr);
    receivedMessage.reserve(1024);

    while (_running) {
        removeInactiveUsers();

        // Create a future for the ping
        auto send_future = sendPing();

        // Create a future for the receive operation
        auto recv_future = receivePong(n, len);

        // Wait for both futures to complete
        send_future.get();
        recv_future.get();

        if (ntohs(cliaddr.sin_port) == _uPort) {
            continue;
        }
        if (receivedMessage.find(PONG_PREFIX) != std::string::npos) {
            std::string token = receivedMessage.substr(PONG_PREFIX.size());
            if (_users.find(token) == _users.end()) {
                _users[token] = createUser(token, cliaddr);
            }
            _users[token]->lastHeartbeat = std::chrono::system_clock::now();
        }

        std::string response = PONG_PREFIX + _token;
        sendto(_uSocket, (const char *)response.c_str(), response.size(),
            MSG_CONFIRM, (const struct sockaddr *) &cliaddr, len);
    }
    close(_uSocket);
}


std::future<int> NetManager::sendPing() {
    return std::async(std::launch::async, [&]() {
        servaddr.sin_addr.s_addr = inet_addr("224.0.0.1"); // replace with your multicast address
        servaddr.sin_port = htons(_uPort);
        const char* message = "ping";

        // Set the multicast TTL
        unsigned char multicastTTL = 3; // adjust as needed
        if (setsockopt(_uSocket, IPPROTO_IP, IP_MULTICAST_TTL, (void *) &multicastTTL, sizeof(multicastTTL)) < 0) {
            perror("setsockopt - IP_MULTICAST_TTL");
            return -1;
        }

        int bytesSent = sendto(_uSocket, message, strlen(message),
            MSG_CONFIRM, (const struct sockaddr *) &servaddr, 
            sizeof(servaddr));
        if (bytesSent == -1) {
            perror("sendto error");
        }
        return bytesSent;
    });
}

std::future<int> NetManager::receivePong(int& n, int& len) {
    return std::async(std::launch::async, [&]() {
        servaddr.sin_addr.s_addr = INADDR_ANY;
        char buffer[1024];
        len = sizeof(cliaddr);

        n = recvfrom(_uSocket, buffer, sizeof(buffer) - 1, MSG_WAITALL,
        (struct sockaddr *) &cliaddr, (socklen_t*)&len);
        if (n >= 0) {
            buffer[n] = '\0';
            receivedMessage.assign(buffer);
        } else {
            if (errno == EWOULDBLOCK) {
                // timeout, no data received
                return n;
            } else {
                perror("recvfrom error");
                _running = false;
            }
        }
        return n;
    });
}
