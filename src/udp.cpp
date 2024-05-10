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
    char buffer[1024];
    std::string receivedMessage;
    receivedMessage.reserve(1024);
    int port = TLSS_C::PORT+10;
    int endPort = TLSS_C::PORT+20;

    while (_running) {
        removeInactiveUsers();

        // Create a future for the ping
        auto send_future = std::async(std::launch::async, [&]() {
            servaddr.sin_addr.s_addr = INADDR_BROADCAST;
            port > endPort ? port = TLSS_C::PORT+10 : port++;
            servaddr.sin_port = htons(port);
            const char* message = "ping";
            int bytesSent = sendto(_uSocket, message, strlen(message),
                MSG_CONFIRM, (const struct sockaddr *) &servaddr, 
                sizeof(servaddr));
            if (bytesSent == -1) {
                perror("sendto error");
            }
        });

        // Create a future for the receive operation
        auto recv_future = std::async(std::launch::async, [&]() {
            servaddr.sin_addr.s_addr = INADDR_ANY;
            n = recvfrom(_uSocket, buffer, sizeof(buffer) - 1, MSG_WAITALL,
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

        if (receivedMessage.find(PONG_PREFIX) != std::string::npos) {
            std::string token = receivedMessage.substr(PONG_PREFIX.size());
            if (_users.find(token) == _users.end()) {
                _users[token] = createUser(token, cliaddr);
            }
            _users[token]->lastHeartbeat = std::chrono::system_clock::now();
        }

        if (ntohs(cliaddr.sin_port) == _uPort) {
            continue;
        }

        std::string response = PONG_PREFIX + _token;
        sendto(_uSocket, (const char *)response.c_str(), response.size(),
            MSG_CONFIRM, (const struct sockaddr *) &cliaddr, len);
    }
    close(_uSocket);
}

